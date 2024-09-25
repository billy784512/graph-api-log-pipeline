using System.Net;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

using Azure.Storage.Blobs;

using Newtonsoft.Json;

using daemon_console;
using global_class;

namespace AbnormalMeetings
{
    public class SubscriptionRenewalService
    {
        private readonly ILogger _logger;
        private static readonly AuthenticationConfig _config = LoadAuthenticationConfig();
        private readonly string? CONNECTION_STRING = Environment.GetEnvironmentVariable("BLOB_CONNECTION_STRING");
        private readonly string? FUNCTION_APP_NAME = Environment.GetEnvironmentVariable("FUNCTION_APP_NAME").ToLower();
        private readonly string? FUNCTION_DEFAULT_KEY = Environment.GetEnvironmentVariable("FUNCTION_DEFAULT_KEY"); 
        private const string CALL_RECORD_ID = "callRecordId";


        public SubscriptionRenewalService(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<SubscriptionRenewalService>();
        }
        private static AuthenticationConfig LoadAuthenticationConfig()
        {
            return new AuthenticationConfig
            {
                Tenant = Environment.GetEnvironmentVariable("TENANT_ID"),
                ClientId = Environment.GetEnvironmentVariable("CLIENT_ID"),
                ClientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET"),
            };
        }


        [Function("SubscriptionRenewalCronjob")]
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
                _logger.LogError($"Error in SubscriptionRenewal(cronjob): {ex.Message}");
            }
        }

        [Function("SubscriptionRenewalHttp")]
        public async Task<HttpResponseData> RunHttp([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req){
            _logger.LogInformation("SubscriptionRenewal(http) executed");

            try
            {
                var scopes = new[] { $"{_config.ApiUrl}.default" };

                _logger.LogInformation("Running Function: CallMSGraphAsync");
                await CallMSGraphAsync(scopes);

                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteStringAsync("SubscriptionRenewal executed successfully.");
                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in RenewSubscription(http): {ex.Message}");
                var res = req.CreateResponse(HttpStatusCode.BadRequest);
                await res.WriteStringAsync("Error in SubscriptionRenewal, please check log.");
                return res;
            }
        }

        private async Task CallMSGraphAsync(string[] scopes)
        {
            try
            {
                var graphServiceClient = GlobalFunction.GetAuthenticatedGraphClient(_config.Tenant, _config.ClientId, _config.ClientSecret, scopes);

                var subscriptionList = await LoadSubscriptionList();

                await RenewOrCreateCallRecordSubscriptions(graphServiceClient, subscriptionList);
                await RenewOrCreateUserEventSubscriptions(graphServiceClient, subscriptionList);
                await SaveSubscriptionList(subscriptionList);
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

        private async Task<SubscriptionList> LoadSubscriptionList()
        {
            var containerClient = new BlobContainerClient(CONNECTION_STRING, _config.BlobContainerName_SubscriptionList);
            containerClient.CreateIfNotExists();

            var blobClient = containerClient.GetBlobClient(_config.BlobFileName);
            if (!await blobClient.ExistsAsync())
            {
                _logger.LogInformation("Subscription list does not exist. Creating a new one.");
                var newList = new SubscriptionList { value = new List<SubscriptionInfo>() };
                await SaveSubscriptionList(newList);
                return newList;
            }

            var response = await blobClient.DownloadAsync();
            using var responseStream = response.Value.Content;
            var responseString = await new StreamReader(responseStream).ReadToEndAsync();
            return JsonConvert.DeserializeObject<SubscriptionList>(responseString);
        }

        private async Task SaveSubscriptionList(SubscriptionList subscriptionList)
        {
            var jsonString = System.Text.Json.JsonSerializer.Serialize(subscriptionList);
            await GlobalFunction.SaveToBlobContainer(_config.BlobFileName, jsonString, CONNECTION_STRING, _config.BlobContainerName_SubscriptionList, _logger);
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
            string changeType = userEventMode ? "created,updated" : "created";
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