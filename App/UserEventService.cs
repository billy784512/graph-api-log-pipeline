using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

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
            string filename = $"{subscriptionData.value[0].resourceData.id}.json";
            string webApiUrl = $"{_config.ApiUrl}v1.0/{resource}";

            IConfidentialClientApplication app;

            try
            {
                _logger.LogInformation("Login to application...");
                app = GlobalFunction.GetAppAsync(_config);
                _logger.LogInformation("Login Successfully.");

                string[] scopes = [$"{_config.ApiUrl}.default"]; // "https://graph.microsoft.com/.default"

                string userEventJson = await GlobalFunction.GetHttpRequest(app, scopes, webApiUrl, _logger);

                string containerName = _config.BlobContainerName_UserEvents;
                await GlobalFunction.SaveToBlob(filename, userEventJson, CONNECTION_STRING, containerName, _logger);

                var res = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await res.WriteStringAsync("SaveSubscription done");
                return res;
            }
            catch (Exception ex){
                _logger.LogError(ex.Message);
                var res = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await res.WriteStringAsync("SaveSubscription failed");
                return res;
            }
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