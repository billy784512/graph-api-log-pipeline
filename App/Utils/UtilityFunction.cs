using System.Text;
using System.Net.Http.Headers;

using Microsoft.Graph;
using Microsoft.Identity.Web;
using Microsoft.Identity.Client;
using Microsoft.Extensions.Logging;

using Azure.Storage.Blobs;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker.Http;

namespace App.Utils
{
    public class UtilityFunction
    {
        public static void PrintHeaders(HttpHeadersCollection reqHeaders, ILogger log)
        {
            var headersInfo = reqHeaders
                .Select(header => $"Key: {header.Key}\tValue: {header.Value}")
                .Aggregate("Request Headers:\n", (acc, header) => acc + header + "\n");

            log.LogInformation(headersInfo);
        }

        public static async Task PrintBody(HttpRequestData req, ILogger log)
        {
            using var reader = new StreamReader(req.Body);
            string body = await reader.ReadToEndAsync();

            log.LogInformation($"Request Body: {body}"); 
        }

        // Acquires an access token (from given scope).
        // Then, makes a GET method request to the target API.
        public static async Task<string> GetHttpRequest(IConfidentialClientApplication app, string[] scopes, string webApiUrl, ILogger log)
        {
            try
            {
                var result = await app.AcquireTokenForClient(scopes).ExecuteAsync();
                log.LogInformation("Token acquired");

                using var httpClient = new HttpClient();

                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await httpClient.GetAsync(webApiUrl);

                if (!response.IsSuccessStatusCode){
                    log.LogWarning($"Failed to call the web API: {response.StatusCode}");
                    log.LogWarning($"Content: {await response.Content.ReadAsStringAsync()}");
                    return null;
                }

                return await response.Content.ReadAsStringAsync();
            }
            catch (MsalServiceException ex) when (ex.Message.Contains("AADSTS70011"))
            {
                log.LogError("Scope provided is not supported.");
            }
            catch (Exception ex)
            {
                log.LogError($"An error occurred: {ex.Message}");
            }
            return null;
        }

        public static GraphServiceClient GetAuthenticatedGraphClient(string tenantId, string clientId, string clientSecret, string[] scopes)
        {
            // Create the credential using Azure.Identity
            var clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret);

            // Pass the credential and scopes to the GraphServiceClient
            var graphClient = new GraphServiceClient(clientSecretCredential, scopes);

            return graphClient;
        }

        // Initializes and returns a ConfidentialClientApplication.
        public static IConfidentialClientApplication GetAppAsync(AuthenticationConfig config)
        {
            IConfidentialClientApplication app;

            app = ConfidentialClientApplicationBuilder.Create(config.ClientId)
                .WithClientSecret(config.ClientSecret)
                .WithAuthority(new Uri(config.Authority))
                .Build();

            _ = app.AddInMemoryTokenCache();

            return app;
        }

        public static async Task SaveToBlobContainer(string file_name, string jsonString, string connectionString, string containerName, ILogger log)
        {

            BlobContainerClient container = new BlobContainerClient(connectionString, containerName);
            _ = container.CreateIfNotExists();

            var blob = container.GetBlobClient(file_name);
            using (MemoryStream mem = new MemoryStream())
            {
                // Write to stream
                Byte[] info = new UTF8Encoding(true).GetBytes(jsonString);
                mem.Write(info, 0, info.Length);

                // Go back to beginning of stream
                mem.Position = 0;

                // Upload the file to the server
                await blob.UploadAsync(mem, overwrite: true);
            }
        }

    }
}
