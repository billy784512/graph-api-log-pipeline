using System.Net;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

using Azure.Storage.Blobs;

using Newtonsoft.Json;

using App.Utils;
using App.Models;

namespace App
{
    public class SubscriptionService
    {
        private readonly ILogger _logger;
        private readonly AuthenticationConfig _config;

        private readonly string? BLOB_CONNECTION_STRING = Environment.GetEnvironmentVariable("BLOB_CONNECTION_STRING");
        private readonly string? FUNCTION_APP_NAME = Environment.GetEnvironmentVariable("FUNCTION_APP_NAME").ToLower();
        private readonly string? FUNCTION_DEFAULT_KEY = Environment.GetEnvironmentVariable("FUNCTION_DEFAULT_KEY"); 
        private readonly string CALL_RECORD_ID = "callRecordId";

        public SubscriptionService(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<SubscriptionService>();
            _config = new AuthenticationConfig
            {
                Tenant = Environment.GetEnvironmentVariable("TENANT_ID"),
                ClientId = Environment.GetEnvironmentVariable("CLIENT_ID"),
                ClientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET"),
            };
        }
        

        [Function("SubscriptionServiceCronjob")]
        public async Task RunTimer([TimerTrigger("0 0 16 * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"SubscriptionRenewal(cronjob) executed at: {DateTime.Now}");

            try
            {
                var scopes = new[] { $"{_config.ApiUrl}.default" };

                _logger.LogInformation("Running Function: CallMSGraphAsync");
                await CallMSGraphAsync(scopes);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to excute subscriptionRenewal(cronjob): {ex.Message}");
            }
        }

        [Function("SubscriptionServiceHttp")]
        public async Task<HttpResponseData> RunHttp([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req){
            _logger.LogInformation("SubscriptionRenewal(http) executed");

            try
            {
                var scopes = new[] { $"{_config.ApiUrl}.default" };

                _logger.LogInformation("Running Function: CallMSGraphAsync");
                await CallMSGraphAsync(scopes);

                return await UtilityFunction.MakeResponse(req, HttpStatusCode.OK, "SubscriptionRenewal executed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in RenewSubscription(http): {ex.Message}");
                return await UtilityFunction.MakeResponse(req, HttpStatusCode.BadRequest, $"Failed to excute subscriptionRenewal(http): {ex.Message}");
            }
        }

        private async Task CallMSGraphAsync(string[] scopes)
        {
            try
            {
                var graphServiceClient = UtilityFunction.GetAuthenticatedGraphClient(_config.Tenant, _config.ClientId, _config.ClientSecret, scopes);
                var containerClient = new BlobContainerClient(BLOB_CONNECTION_STRING, _config.BlobContainerName_SubscriptionList);
                containerClient.CreateIfNotExists();

                var subscriptionList = await LoadSubscriptionList(containerClient);

                await RenewOrCreateCallRecordSubscriptions(graphServiceClient, subscriptionList);
                await RenewOrCreateUserEventSubscriptions(graphServiceClient, subscriptionList);
                await SaveSubscriptionList(containerClient, subscriptionList);
            }
            catch (ServiceException e)
            {
                _logger.LogError($"graphServiceClient error: {e.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in CallMSGraphAsync: {ex.Message}");
            }
        }

        private async Task<SubscriptionList> LoadSubscriptionList(BlobContainerClient containerClient)
        {
            var blobClient = containerClient.GetBlobClient(_config.SubscriptionListFileName);
            if (!await blobClient.ExistsAsync())
            {
                _logger.LogInformation("Subscription list does not exist. Creating a new one.");
                var newList = new SubscriptionList { value = new List<SubscriptionInfo>() };
                await SaveSubscriptionList(containerClient, newList);
                return newList;
            }

            var response = await blobClient.DownloadAsync();
            using var responseStream = response.Value.Content;
            var responseString = await new StreamReader(responseStream).ReadToEndAsync();
            return JsonConvert.DeserializeObject<SubscriptionList>(responseString);
        }

        private async Task SaveSubscriptionList(BlobContainerClient containerClient, SubscriptionList subscriptionList)
        {
            var jsonPayload = System.Text.Json.JsonSerializer.Serialize(subscriptionList);
            await UtilityFunction.SaveToBlobContainer(containerClient, jsonPayload, _config.SubscriptionListFileName);
            _logger.LogInformation("Subscription list saved successfully.");
        }

        private async Task RenewOrCreateCallRecordSubscriptions(GraphServiceClient graphServiceClient, SubscriptionList subscriptionList){
            _logger.LogInformation("Renew or Create CallRecord subscription");

            var subscriptionDict = subscriptionList.value.ToDictionary(sub => sub.UserId, sub => sub.SubscriptionId);

            // Renew
            if (subscriptionDict.ContainsKey(CALL_RECORD_ID)){
                await RenewSubscription(graphServiceClient, subscriptionDict[CALL_RECORD_ID]);
                return;
            }

            // Create
            var subscription = MakeSubscriptionObject(false);
            Subscription responseSubscription = await CreateSubscription(graphServiceClient, subscription);

            if (responseSubscription != null){
                SubscriptionInfo subscriptionInfo = new SubscriptionInfo(CALL_RECORD_ID, responseSubscription.Id);
                subscriptionList.value.Add(subscriptionInfo);
            }
        }

        private async Task RenewOrCreateUserEventSubscriptions(GraphServiceClient graphServiceClient, SubscriptionList subscriptionList)
        {
            var subscriptionDict = subscriptionList.value.ToDictionary(sub => sub.UserId, sub => sub.SubscriptionId);


            _logger.LogInformation("Fetching users from Graph API...");
            var users = await graphServiceClient.Users.GetAsync();
            _logger.LogInformation($"Found {users.Value.Count} users.");


            foreach (var user in users.Value)
            {
                _logger.LogInformation($"Processing user: {user.DisplayName} (ID: {user.Id})");
                
                if (subscriptionDict.ContainsKey(user.Id))
                {
                    await RenewSubscription(graphServiceClient, subscriptionDict[user.Id]);
                    continue;
                }

                var subscription = MakeSubscriptionObject(true, user.Id);
                Subscription responseSubscription = await CreateSubscription(graphServiceClient, subscription);

                if (responseSubscription != null){
                    SubscriptionInfo subscriptionInfo = new SubscriptionInfo(user.Id, responseSubscription.Id);
                    subscriptionList.value.Add(subscriptionInfo);
                }
            }
        }

        private Subscription MakeSubscriptionObject(bool userEventMode, string? userId=null){

            string Resource = userEventMode ? $"/users/{userId}/events" : "/communications/callRecords";
            string changeType = userEventMode ? "created" : "created";
            string urlCode = userEventMode ? "UserEvent" : "CallRecord";

            string endpointTemplateString = "https://{0}.azurewebsites.net/api/{1}?code={2}&clientId=default";
            string webhookUrl = String.Format(endpointTemplateString, FUNCTION_APP_NAME, urlCode, FUNCTION_DEFAULT_KEY);

            return new Subscription{
                ChangeType = changeType,
                NotificationUrl = webhookUrl,
                Resource = Resource,
                ExpirationDateTime = DateTime.UtcNow.AddMinutes(1440*2), //2 days
                ClientState = "secretClientValue",
                LatestSupportedTlsVersion = "v1_2"
            };
        }

        private async Task RenewSubscription(GraphServiceClient graphServiceClient, string subscriptionId)
        {
            try
            {
                _logger.LogInformation($"Renewing subscription: {subscriptionId}");
                var subscriptionToUpdate = new Subscription
                {
                    ExpirationDateTime = DateTime.UtcNow.AddDays(2),
                };
                await graphServiceClient.Subscriptions[subscriptionId].PatchAsync(subscriptionToUpdate);
                _logger.LogInformation("Subscription renewed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error renewing subscription: {ex.Message}");
            }
        }

        private async Task<Subscription> CreateSubscription(GraphServiceClient graphServiceClient, Subscription subscription)
        {
            try
            {
                var responseSubscription = await graphServiceClient.Subscriptions.PostAsync(subscription);
                _logger.LogInformation("Subscription created successfully.");
                return responseSubscription;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating subscription: {ex.Message}");
                return null;
            }
        }
    }
}