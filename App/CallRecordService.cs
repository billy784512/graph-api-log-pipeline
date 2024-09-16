using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Graph;
using Microsoft.Graph.Models.CallRecords;
using Microsoft.Identity.Client;

using Newtonsoft.Json;

using daemon_console;
using global_class;

namespace AbnormalMeetings
{
    public class CallRecordService
    {
        private readonly ILogger _logger;
        private static readonly AuthenticationConfig _config = LoadAuthenticationConfig();
        private string? CONNECTION_STRING = Environment.GetEnvironmentVariable("BLOB_CONNECTION_STRING");
        public CallRecordService(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<CallRecordService>();
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


        [Function("CallRecord")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestData req){
            _logger.LogInformation("CallRecordService is triggered.");

            GlobalFunction.PrintHeaders(req.Headers, _logger);
            await GlobalFunction.PrintBody(req, _logger);

            bool isValidationProcess = req.Query["validationToken"] != null;
            if (isValidationProcess){
                return await ValidationResponse(req);
            }
            return await SaveSubscription(req);
        }

        private async Task<HttpResponseData> SaveSubscription(HttpRequestData req){
            string reqBody = await new StreamReader(req.Body).ReadToEndAsync();
            SubscriptionData subscriptionData = JsonConvert.DeserializeObject<SubscriptionData>(reqBody);

            string meetingID = subscriptionData.value[0].resourceData.id;
            //string filename = $"{subscriptionData.value[0].resourceData.id}.json";
            //string webApiUrl = $"{_config.ApiUrl}v1.0/{resource}";

            IConfidentialClientApplication app;

            try{
                //_logger.LogInformation("Login to application...");
                //app = GlobalFunction.GetAppAsync(_config);
                //_logger.LogInformation("Login Successfully.");

                string[] scopes = [$"{_config.ApiUrl}.default"]; //"https://graph.microsoft.com/.default"

                CallRecord callrecord = await GetCallRecordsfromGraphSDK(scopes, meetingID);

                string filename = callrecord.Id + ".json";
                string jsonString = System.Text.Json.JsonSerializer.Serialize(callrecord);
                string containerName = _config.BlobContainerName_CallRecords;

                await GlobalFunction.SaveToBlob(filename, jsonString, CONNECTION_STRING, containerName, _logger);

                var res = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await res.WriteStringAsync("SaveSubscription done");
                return res;
            }
            catch (Exception ex){
                _logger.LogError(ex.Message);
            }

            var badRes = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badRes.WriteStringAsync("SaveSubscription failed");
            return badRes;
        }

        private async Task<CallRecord> GetCallRecordsfromGraphSDK(string[] scopes, string call_Id)
        {
            // Prepare an authenticated MS Graph SDK client
            GraphServiceClient graphServiceClient = daemon_console.GlobalFunction.GetAuthenticatedGraphClient(_config.Tenant, _config.ClientId, _config.ClientSecret, scopes);

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


        private async Task<HttpResponseData> ValidationResponse(HttpRequestData req){ 
            string validationToken = req.Query["validationToken"];
            _logger.LogInformation($"validationToken: {validationToken}");

            var res = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await res.WriteStringAsync($"{validationToken}");
            return res;
        }
    }
}