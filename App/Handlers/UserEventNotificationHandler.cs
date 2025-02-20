using System.Reflection;
using System.Text.RegularExpressions;

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


namespace App.Handlers
{
    public class UserEventNotificationHandler
    {
        private readonly ILogger _logger;
        private readonly AppConfig _config;

        private readonly GraphApiRequestHandler _graphApiRequestHandler;
        private readonly EventHubProducerClient _producerClient;
        private readonly BlobContainerClient _containerClient;

        public UserEventNotificationHandler(
            GraphApiRequestHandler graphApiRequestHandler,
            EventHubProducerClientFactory eventHubProducerClientFactory, 
            BlobContainerClientFactory blobContainerClientFactory, 
            AppConfig config, 
            ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<UserEventNotificationHandler>();
            _config = config;

            _graphApiRequestHandler = graphApiRequestHandler;
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

            // Parse HTTP payload to SubscriptionData object
            SubscriptionData subscriptionData;
            try
            {
                subscriptionData = JsonConvert.DeserializeObject<SubscriptionData>(reqBody);
            }
            catch(JsonException ex)
            {
                _logger.LogError(ErrorMessage.ERR_METHOD_EXECUTE, UtilityFunction.GetCurrentMethodName(), ex.Message);
                return await UtilityFunction.MakeResponse(req, System.Net.HttpStatusCode.BadRequest, $"Failed to deserialize request body: {ex.Message}.");
            }


            // Extract IDs from SubscriptionData object
            string resource = subscriptionData.value[0].resource;
            string pattern = @"Users/([^/]+)/Events/([^/]+)";

            Match match = Regex.Match(resource, pattern);
            if (! match.Success)
            {
                _logger.LogError("Regex match failed, raw data: {data}", resource);
                return await UtilityFunction.MakeResponse(req, System.Net.HttpStatusCode.BadRequest, "Regex match failed.");
            }

            string userId = match.Groups[1].Value;
            string eventId = match.Groups[2].Value;


            // Get log via GraphAPI, then send log to target destination
            try
            {
                Event calendarEvent = await _graphApiRequestHandler.GetUserEvent(userId, eventId);

                string fileName = $"{subscriptionData.value[0].resourceData.id}.json";
                string jsonPayload = JsonConvert.SerializeObject(calendarEvent);

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
                _logger.LogError(ErrorMessage.ERR_METHOD_EXECUTE, UtilityFunction.GetCurrentMethodName(), ex.Message);
                return await UtilityFunction.MakeResponse(req, System.Net.HttpStatusCode.BadRequest, $"Failed to send log: {ex.Message}");
            }
        }
    }
}