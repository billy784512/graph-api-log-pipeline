using System.Text.RegularExpressions;

using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

using Azure.Storage.Blobs;
using Azure.Messaging.EventHubs.Producer;

using Newtonsoft.Json;

using App.Utils;
using App.Models;
using App.Factory;


namespace App
{
    public class UserEventNotificationHandler
    {
        private readonly ILogger _logger;
        private readonly AppConfig _config;

        private readonly GraphServiceClient _graphServiceClient;
        private readonly EventHubProducerClient _producerClient;
        private readonly BlobContainerClient _containerClient;

        public UserEventNotificationHandler(
            GraphServiceClient graphServiceClient,
            EventHubProducerClientFactory eventHubProducerClientFactory, 
            BlobContainerClientFactory blobContainerClientFactory, 
            AppConfig config, 
            ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<UserEventNotificationHandler>();
            _config = config;

            _graphServiceClient = graphServiceClient;
            _producerClient = eventHubProducerClientFactory.GetClient(_config.EventHubTopic_UserEvents);
            _containerClient = blobContainerClientFactory.GetClient(_config.BlobContainerName_UserEvents);

            _containerClient.CreateIfNotExists();
        }


        [Function("UserEventNotificationHandler")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestData req){
            bool isValidationProcess = req.Query["validationToken"] != null;
            if (isValidationProcess){
                return await UtilityFunction.GraphNotificationValidationResponse(req);
            }
            return await SendEvent(req);
        }

        private async Task<HttpResponseData> SendEvent(HttpRequestData req){
            string reqBody = await new StreamReader(req.Body).ReadToEndAsync();
            SubscriptionData subscriptionData;
            try
            {
                subscriptionData = JsonConvert.DeserializeObject<SubscriptionData>(reqBody);
            }
            catch(JsonException ex)
            {
                _logger.LogError($"Failed to deserialize request body: {ex.Message}");
                return await UtilityFunction.MakeResponse(req, System.Net.HttpStatusCode.BadRequest, $"Failed to deserialize request body: {ex.Message}.");
            }

            string resource = subscriptionData.value[0].resource;
            string pattern = @"Users/([^/]+)/Events/([^/]+)";

            Match match = Regex.Match(resource, pattern);
            if (! match.Success)
            {
                _logger.LogError($"Regex match failed, raw data: {resource}");
                return await UtilityFunction.MakeResponse(req, System.Net.HttpStatusCode.BadRequest, "Regex match failed.");
            }

            string userId = match.Groups[1].Value;
            string eventId = match.Groups[2].Value;

            try
            {
                Event calendarEvent = await GetUserEventFromGraphSDK(userId, eventId);

                string fileName = $"{subscriptionData.value[0].resourceData.id}.json";
                string jsonPayload = System.Text.Json.JsonSerializer.Serialize(calendarEvent);

                bool toggle = Convert.ToBoolean(_config.EVENT_HUB_FEATURE_TOGGLE);

                if (toggle){
                    await UtilityFunction.SendToEventHub(_producerClient, jsonPayload, fileName);
                    return await UtilityFunction.MakeResponse(req, System.Net.HttpStatusCode.Accepted, "Send log to Event Hub successfully.");
                }
                else{
                    await UtilityFunction.SaveToBlobContainer(_containerClient, jsonPayload, fileName);
                    return await UtilityFunction.MakeResponse(req, System.Net.HttpStatusCode.Accepted, "Save log to Sotrage Account successfully.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return await UtilityFunction.MakeResponse(req, System.Net.HttpStatusCode.BadRequest, $"Failed to redirect logs: {ex.Message}");
            }
        }

        private async Task<Event> GetUserEventFromGraphSDK(string userId, string eventId)
        {
            try
            {
                var userEvent = await _graphServiceClient.Users[userId].Events[eventId]
                    .GetAsync();
                    
                return userEvent;
            }
            catch (ServiceException e)
            {
                _logger.LogInformation("GetUserEventfromGraphSDK Failed: " + $"{e}");
            }

            return null;
        }
    }
}