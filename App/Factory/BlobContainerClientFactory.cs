using Azure.Storage.Blobs;

using App.Utils;


namespace App.Factory
{
    public class BlobContainerClientFactory
    {
        private readonly Dictionary<string, BlobContainerClient> _clients;

        public BlobContainerClientFactory(AppConfig config){
            _clients = new Dictionary<string, BlobContainerClient>
            {
                {
                    config.BlobContainerName_ChatMessages,
                    new BlobContainerClient(config.BLOB_CONNECTION_STRING, config.BlobContainerName_ChatMessages)
                },
                {
                    config.BlobContainerName_CallRecords,
                    new BlobContainerClient(config.BLOB_CONNECTION_STRING, config.BlobContainerName_CallRecords)
                },
                {
                    config.BlobContainerName_UserEvents,
                    new BlobContainerClient(config.BLOB_CONNECTION_STRING, config.BlobContainerName_UserEvents)
                },
                {
                    config.BlobContainerName_SubscriptionList,
                    new BlobContainerClient(config.BLOB_CONNECTION_STRING, config.BlobContainerName_SubscriptionList)
                }
            };
        }

        public BlobContainerClient GetClient(string containerName){
            if (_clients.TryGetValue(containerName, out var client))
            {
                return client;
            }

            throw new KeyNotFoundException($"No Event Hub producer client found for {containerName}");
        }
    }
}