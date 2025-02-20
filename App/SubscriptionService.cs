using System.Reflection;
using System.Net;

using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

using Azure.Storage.Blobs;

using Newtonsoft.Json;

using App.Utils;
using App.Models;
using App.Factory;
using App.Handlers;


namespace App
{
    public class SubscriptionService
    {
        private readonly ILogger _logger;
        private readonly AppConfig _config;

        private readonly GraphApiRequestHandler _graphApiRequestHandler;
        private readonly BlobContainerClient _containerClient;

        public SubscriptionService(
            GraphApiRequestHandler graphApiRequestHandler,
            BlobContainerClientFactory blobContainerClientFactory, 
            AppConfig config, 
            ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<SubscriptionService>();
            _config = config;

            _graphApiRequestHandler = graphApiRequestHandler;
            _containerClient = blobContainerClientFactory.GetClient(_config.BlobContainerName_SubscriptionList);
            _containerClient.CreateIfNotExists();
        }
        

        [Function("SubscriptionServiceCronjob")]
        public async Task RunTimer([TimerTrigger("0 0 16 * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"SubscriptionRenewal(cronjob) executed at: {DateTime.Now}");
            try
            {
                await CallMSGraphAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ErrorMessage.ERR_METHOD_EXECUTE, UtilityFunction.GetCurrentMethodName(), ex.Message);
            }
        }

        [Function("SubscriptionServiceHttp")]
        public async Task<HttpResponseData> RunHttp([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req){
            try
            {
                await CallMSGraphAsync();
                return await UtilityFunction.MakeResponse(req, HttpStatusCode.OK, "SubscriptionRenewal executed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ErrorMessage.ERR_METHOD_EXECUTE, UtilityFunction.GetCurrentMethodName(), ex.Message);
                return await UtilityFunction.MakeResponse(req, HttpStatusCode.BadRequest, $"Failed to excute subscriptionRenewal(http): {ex.Message}");
            }
        }

        private async Task CallMSGraphAsync()
        {
            try
            {
                var subscriptions = await LoadSubscriptions();
                await ProcessCallRecordSubscriptions(subscriptions);
                await ProcessUserEventSubscriptions(subscriptions);
                await SaveSubscriptions(subscriptions);
            }
            catch (ServiceException e)
            {
                _logger.LogError($"graphServiceClient error: {e.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ErrorMessage.ERR_METHOD_EXECUTE, UtilityFunction.GetCurrentMethodName(), ex.Message);
            }
        }

        private async Task<SubscriptionList> LoadSubscriptions()
        {
            var blobClient = _containerClient.GetBlobClient(_config.SubscriptionListFileName);

            if (!await blobClient.ExistsAsync())
            {
                _logger.LogInformation("No activate subscriptions. Creating an empty list now...");
                var newList = new SubscriptionList { value = new List<SubscriptionInfo>() };
                await SaveSubscriptions(newList);
                return newList;
            }

            var response = await blobClient.DownloadAsync();
            using var responseStream = response.Value.Content;
            var responseString = await new StreamReader(responseStream).ReadToEndAsync();
            return JsonConvert.DeserializeObject<SubscriptionList>(responseString);
        }

        private async Task SaveSubscriptions(SubscriptionList subscriptions)
        {
            var jsonPayload = JsonConvert.SerializeObject(subscriptions);
            await UtilityFunction.SaveToBlobContainer(_containerClient, jsonPayload, _config.SubscriptionListFileName);
            _logger.LogInformation("Subscriptions saved successfully.");
        }

        private async Task ProcessCallRecordSubscriptions(SubscriptionList subscriptions){
            _logger.LogInformation("Renew or Create CallRecord subscription");

            var subscriptionDict = subscriptions.value.ToDictionary(sub => sub.UserId, sub => sub.SubscriptionId);

            // Renew
            if (subscriptionDict.ContainsKey(_config.CallRecordId)){
                await _graphApiRequestHandler.RenewSubscription(subscriptionDict[_config.CallRecordId]);
                return;
            }

            // Create
            var subscription = MakeSubscriptionObject(false);
            Subscription responseSubscription = await _graphApiRequestHandler.CreateSubscription(subscription);

            if (responseSubscription != null){
                SubscriptionInfo subscriptionInfo = new SubscriptionInfo(_config.CallRecordId, responseSubscription.Id);
                subscriptions.value.Add(subscriptionInfo);
            }
        }

        private async Task ProcessUserEventSubscriptions(SubscriptionList subscriptionList)
        {
            var subscriptionDict = subscriptionList.value.ToDictionary(sub => sub.UserId, sub => sub.SubscriptionId);


            _logger.LogInformation("Fetching users from Graph API...");
            var users = await _graphApiRequestHandler.GetUSer();
            _logger.LogInformation($"Found {users.Value.Count} users.");


            foreach (var user in users.Value)
            {
                _logger.LogInformation($"Processing user: {user.DisplayName} (ID: {user.Id})");
                
                if (subscriptionDict.ContainsKey(user.Id))
                {
                    await _graphApiRequestHandler.RenewSubscription(subscriptionDict[user.Id]);
                    continue;
                }

                var subscription = MakeSubscriptionObject(true, user.Id);
                Subscription responseSubscription = await _graphApiRequestHandler.CreateSubscription(subscription);

                if (responseSubscription != null){
                    SubscriptionInfo subscriptionInfo = new SubscriptionInfo(user.Id, responseSubscription.Id);
                    subscriptionList.value.Add(subscriptionInfo);
                }
            }
        }

        private Subscription MakeSubscriptionObject(bool userEventMode, string? userId=null){

            string Resource = userEventMode ? $"/users/{userId}/events" : "/communications/callRecords";
            string changeType = userEventMode ? "created" : "created";
            string urlCode = userEventMode ? "UserEventNotificationHandler" : "CallRecordNotificationHandler";

            string endpointTemplateString = "https://{0}.azurewebsites.net/api/{1}?code={2}&clientId=default";
            string webhookUrl = String.Format(endpointTemplateString, _config.FUNCTION_APP_NAME, urlCode, _config.FUNCTION_DEFAULT_KEY);

            return new Subscription{
                ChangeType = changeType,
                NotificationUrl = webhookUrl,
                Resource = Resource,
                ExpirationDateTime = DateTime.UtcNow.AddMinutes(1440*2), //2 days
                ClientState = "secretClientValue",
                LatestSupportedTlsVersion = "v1_2"
            };
        }
    }
}