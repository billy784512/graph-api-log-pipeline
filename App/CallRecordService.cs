using System.Net;

using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Graph;
using Microsoft.Graph.Models.CallRecords;

using Azure.Storage.Blobs;
using Azure.Messaging.EventHubs.Producer;

using Newtonsoft.Json;

using App.Utils;
using App.Models;

namespace App
{
    public class CallRecordService
    {
        private readonly ILogger _logger;
        private readonly AuthenticationConfig _config;

        private EventHubProducerClient _producerClient;
        private BlobContainerClient _containerClient;

        private readonly string? BLOB_CONNECTION_STRING = Environment.GetEnvironmentVariable("BLOB_CONNECTION_STRING");
        private readonly string? EVENT_HUB_CONNECTION_STRING = Environment.GetEnvironmentVariable("EVENT_HUB_CONNECTION_STRING");
        private readonly string? EVENT_HUB_FEATURE_TOGGLE = Environment.GetEnvironmentVariable("EVENT_HUB_FEATURE_TOGGLE");
        
        public CallRecordService(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<CallRecordService>();
            _config = new AuthenticationConfig{
                Tenant = Environment.GetEnvironmentVariable("TENANT_ID"),
                ClientId = Environment.GetEnvironmentVariable("CLIENT_ID"),
                ClientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET"),
            };
            _producerClient = new EventHubProducerClient(EVENT_HUB_CONNECTION_STRING, _config.EventHubTopic_CallRecords);
            _containerClient = new BlobContainerClient(BLOB_CONNECTION_STRING, _config.BlobContainerName_CallRecords);
            _containerClient.CreateIfNotExists();
        }
        

        [Function("CallRecordService")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestData req){
            _logger.LogInformation("CallRecordService is triggered.");

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
                string[] scopes = [$"{_config.ApiUrl}.default"];
                CallRecord callrecord = await GetCallRecordsfromGraphSDK(scopes, meetingID);

                string fileName = $"{callrecord.Id}.json";
                string jsonPayload = System.Text.Json.JsonSerializer.Serialize(callrecord);

                bool toggle = Convert.ToBoolean(EVENT_HUB_FEATURE_TOGGLE);

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

        private async Task<CallRecord> GetCallRecordsfromGraphSDK(string[] scopes, string call_Id)
        {
            GraphServiceClient graphServiceClient = UtilityFunction.GetAuthenticatedGraphClient(_config.Tenant, _config.ClientId, _config.ClientSecret, scopes);

            try
            {
                CallRecord callrecord = await graphServiceClient.Communications.CallRecords[call_Id]
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