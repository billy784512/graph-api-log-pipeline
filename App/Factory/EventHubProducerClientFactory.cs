using Azure.Messaging.EventHubs.Producer;

using App.Utils;


namespace App.Factory
{
    public class EventHubProducerClientFactory
    {
        private readonly Dictionary<string, EventHubProducerClient> _clients;

        public EventHubProducerClientFactory(AppConfig config){
            _clients = new Dictionary<string, EventHubProducerClient>
            {
                {
                    config.EventHubTopic_ChatMeesages,
                    new EventHubProducerClient(config.EVENT_HUB_CONNECTION_STRING, config.EventHubTopic_ChatMeesages)
                },
                {
                    config.EventHubTopic_CallRecords,
                    new EventHubProducerClient(config.EVENT_HUB_CONNECTION_STRING, config.EventHubTopic_CallRecords)
                },
                {
                    config.EventHubTopic_UserEvents,
                    new EventHubProducerClient(config.EVENT_HUB_CONNECTION_STRING, config.EventHubTopic_UserEvents)
                }
            };
        }

        public EventHubProducerClient GetClient(string eventHubName){
            if (_clients.TryGetValue(eventHubName, out var client))
            {
                return client;
            }

            throw new KeyNotFoundException($"No Event Hub producer client found for {eventHubName}");
        }
    }
}