using System.Text.RegularExpressions;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

using Azure.Storage.Blobs;
using Azure.Messaging.EventHubs.Producer;

using Newtonsoft.Json;

using App.Utils;
using App.Models;

namespace App
{
    public class UserEventService
    {
        private readonly ILogger _logger;
        private readonly AuthenticationConfig _config;

        private EventHubProducerClient _producerClient;
        private BlobContainerClient _containerClient;

        private readonly string? BLOB_CONNECTION_STRING = Environment.GetEnvironmentVariable("BLOB_CONNECTION_STRING");
        private readonly string? EVENT_HUB_CONNECTION_STRING = Environment.GetEnvironmentVariable("EVENT_HUB_CONNECTION_STRING");
        private readonly string? EVENT_HUB_FEATURE_TOGGLE = Environment.GetEnvironmentVariable("EVENT_HUB_FEATURE_TOGGLE");

        public UserEventService(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<UserEventService>();
            _config = new AuthenticationConfig
            {
                Tenant = Environment.GetEnvironmentVariable("TENANT_ID"),
                ClientId = Environment.GetEnvironmentVariable("CLIENT_ID"),
                ClientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET"),
            };
            _producerClient = new EventHubProducerClient(EVENT_HUB_CONNECTION_STRING, _config.EventHubTopic_UserEvents);
            _containerClient = new BlobContainerClient(BLOB_CONNECTION_STRING, _config.BlobContainerName_UserEvents);
            _containerClient.CreateIfNotExists();
        }


        [Function("UserEvent")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestData req){
            _logger.LogInformation("UserEventService is triggered.");

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
                string[] scopes = [$"{_config.ApiUrl}.default"];
                Event calendarEvent = await GetUserEventfromGraphSDK(scopes, userId, eventId);

                string fileName = $"{subscriptionData.value[0].resourceData.id}.json";
                string jsonPayload = System.Text.Json.JsonSerializer.Serialize(calendarEvent);

                bool toggle = Convert.ToBoolean(EVENT_HUB_FEATURE_TOGGLE);

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

        private async Task<Event> GetUserEventfromGraphSDK(string[] scopes, string userId, string eventId)
        {
            GraphServiceClient graphServiceClient = UtilityFunction.GetAuthenticatedGraphClient(_config.Tenant, _config.ClientId, _config.ClientSecret, scopes);

            try
            {
                var userEvent = await graphServiceClient.Users[userId].Events[eventId]
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