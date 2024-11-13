using System.Net;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Graph;
using Microsoft.Graph.Models.CallRecords;
using Microsoft.Extensions.Logging;

using Azure.Storage.Blobs;
using Azure.Messaging.EventHubs.Producer;

using Newtonsoft.Json;

using App.Utils;
using App.Models;
using App.Factory;


namespace App
{
    public class CallRecordNotificationHandler
    {
        private readonly ILogger _logger;
        private readonly AppConfig _config;

        private readonly GraphServiceClient _graphServiceClient;
        private readonly EventHubProducerClient _producerClient;
        private readonly BlobContainerClient _containerClient;
        
        public CallRecordNotificationHandler(
            GraphServiceClient graphServiceClient, 
            EventHubProducerClientFactory eventHubProducerClientFactory, 
            BlobContainerClientFactory blobContainerClientFactory, 
            AppConfig config, 
            ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<CallRecordNotificationHandler>();
            _config = config;

            _graphServiceClient = graphServiceClient;
            _producerClient = eventHubProducerClientFactory.GetClient(_config.EventHubTopic_UserEvents);
            _containerClient = blobContainerClientFactory.GetClient(_config.BlobContainerName_UserEvents);

            _containerClient.CreateIfNotExists();
        }
        

        [Function("CallRecordNotificationHandler")]
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
                return await UtilityFunction.MakeResponse(req, HttpStatusCode.BadRequest, $"Failed to deserialize request body: {ex.Message}");
            }

            string meetingID = subscriptionData.value[0].resourceData.id;

            try
            {
                CallRecord callrecord = await GetCallRecordsfromGraphSDK(meetingID);

                string fileName = $"{callrecord.Id}.json";
                string jsonPayload = System.Text.Json.JsonSerializer.Serialize(callrecord);

                bool toggle = Convert.ToBoolean(_config.EVENT_HUB_FEATURE_TOGGLE);

                if (toggle){
                    await UtilityFunction.SendToEventHub(_producerClient, jsonPayload, fileName);
                    return await UtilityFunction.MakeResponse(req, HttpStatusCode.Accepted, "Send log to Event Hub successfully.");
                }
                else{
                    await UtilityFunction.SaveToBlobContainer(_containerClient, jsonPayload, fileName);
                    return await UtilityFunction.MakeResponse(req, HttpStatusCode.Accepted, "Save log to Sotrage Account successfully.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return await UtilityFunction.MakeResponse(req, HttpStatusCode.BadRequest, $"Failed to redirect logs: {ex.Message}");
            }
        }

        private async Task<CallRecord> GetCallRecordsfromGraphSDK(string call_Id)
        {
            try
            {
                CallRecord callrecord = await _graphServiceClient.Communications.CallRecords[call_Id]
                    .GetAsync(requestConfiguration => {
                        requestConfiguration.QueryParameters.Expand = new[] { "sessions($expand=segments)"};
                    });
                    
                return callrecord;
            }
            catch (ServiceException e)
            {
                _logger.LogInformation("GetCallRecordsfromGraphSDK Failed: " + $"{e}");
            }
            return null;
        }
    }
}