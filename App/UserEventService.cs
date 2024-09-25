using System.Text.RegularExpressions;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

using Newtonsoft.Json;

using daemon_console;
using global_class;

namespace AbnormalMeetings
{
    public class UserEventService
    {
        private readonly ILogger _logger;
        private static readonly AuthenticationConfig _config = LoadAuthenticationConfig();
        private readonly string? CONNECTION_STRING = Environment.GetEnvironmentVariable("BLOB_CONNECTION_STRING");
        public UserEventService(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<UserEventService>();
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


        [Function("UserEvent")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestData req){
            _logger.LogInformation("UserEventService is triggered.");

            bool isValidationProcess = req.Query["validationToken"] != null;
            if (isValidationProcess){
                return await ValidationResponse(req);
            }
            return await SaveSubscription(req);
        }

        private async Task<HttpResponseData> SaveSubscription(HttpRequestData req){
            string reqBody = await new StreamReader(req.Body).ReadToEndAsync();
            SubscriptionData subscriptionData;
            try
            {
                subscriptionData = JsonConvert.DeserializeObject<SubscriptionData>(reqBody);
            }
            catch(JsonException ex)
            {
                _logger.LogError($"Failed to deserialize request body: {ex.Message}");
                var res = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await res.WriteStringAsync("Invalid request body");
                return res;
            }
            
            string resource = subscriptionData.value[0].resource;
            string pattern = @"Users/([^/]+)/Events/([^/]+)";

            Match match = Regex.Match(resource, pattern);
            if (! match.Success)
            {
                _logger.LogError($"Regex match failed, raw data: {resource}");
                var res = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await res.WriteStringAsync("Regex match failed");
                return res;
            }

            string userId = match.Groups[1].Value;
            string eventId = match.Groups[2].Value;

            try
            {
                string[] scopes = [$"{_config.ApiUrl}.default"];
                Event calendarEvent = await GetUserEventfromGraphSDK(scopes, userId, eventId);

                string filename = $"{subscriptionData.value[0].resourceData.id}.json";
                string jsonString = System.Text.Json.JsonSerializer.Serialize(calendarEvent);
                string containerName = _config.BlobContainerName_UserEvents;

                await GlobalFunction.SaveToBlobContainer(filename, jsonString, CONNECTION_STRING, containerName, _logger);

                var res = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await res.WriteStringAsync("SaveSubscription done");
                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                var res = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await res.WriteStringAsync("SaveSubscription failed");
                return res;
            }
        }

        private async Task<Event> GetUserEventfromGraphSDK(string[] scopes, string userId, string eventId)
        {
            GraphServiceClient graphServiceClient = GlobalFunction.GetAuthenticatedGraphClient(_config.Tenant, _config.ClientId, _config.ClientSecret, scopes);

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

        private async Task<HttpResponseData> ValidationResponse(HttpRequestData req){ 
            string validationToken = req.Query["validationToken"];   
            _logger.LogInformation($"validationToken: {validationToken}");

            var res = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await res.WriteStringAsync($"{validationToken}");
            return res;
        }
    }
}