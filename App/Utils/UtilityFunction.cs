using System.Text;

using Microsoft.Graph;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker.Http;

using Azure.Storage.Blobs;
using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;

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

        public static GraphServiceClient GetAuthenticatedGraphClient(string tenantId, string clientId, string clientSecret, string[] scopes)
        {
            // Create the credential using Azure.Identity
            var clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret);

            // Pass the credential and scopes to the GraphServiceClient
            var graphClient = new GraphServiceClient(clientSecretCredential, scopes);

            return graphClient;
        }

        public static async Task SendToEventHub(EventHubProducerClient producerClient, string jsonPayload, string eventType, string fileName){
            var eventData = new EventData(Encoding.UTF8.GetBytes(jsonPayload));

            eventData.Properties["Format"] = "JSON";
            eventData.Properties["EventType"] = eventType;
            eventData.Properties["FileNane"] = fileName;

            await producerClient.SendAsync(new[] { eventData });
        }

        public static async Task SaveToBlobContainer(BlobContainerClient containerClient, string file_name, string jsonString)
        {
            var blobClient = containerClient.GetBlobClient(file_name);
            using (MemoryStream mem = new MemoryStream())
            {
                // Write to stream
                Byte[] info = new UTF8Encoding(true).GetBytes(jsonString);
                mem.Write(info, 0, info.Length);

                // Go back to beginning of stream
                mem.Position = 0;

                // Upload the file to the server
                await blobClient.UploadAsync(mem, overwrite: true);
            }
        }
    }
}
