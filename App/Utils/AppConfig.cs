namespace App.Utils
{
    public class AppConfig
    {
        /// <summary>
        /// Variables loaded from enviroment vaiable, vaules are determined during DI configuration process
        /// </summary>

        // Feature toggles
        public bool CHAT_API_TOGGLE { get; set; }
        public bool EVENT_HUB_FEATURE_TOGGLE { get; set; }

        // Authentication & authorization credentials
        public string TENANT_ID { get; set; }
        public string CLIENT_ID { get; set; }
        public string CLIENT_SECRET { get; set; }

        // Function App metadata & credentials
        public string FUNCTION_APP_NAME { get; set; }
        public string FUNCTION_DEFAULT_KEY { get; set; }

        // Resource connection string
        public string BLOB_CONNECTION_STRING { get; set; }
        public string EVENT_HUB_CONNECTION_STRING { get; set; }
        

        /// <summary>
        /// Static strings, specifying the storage container or message queue topic  
        /// </summary>
        public string SubscriptionListFileName { get; } = "subscriptionList.json";
        public string CallRecordId { get; } = "callRecord";

        public string BlobContainerName_ChatMessages { get; } = "chatmessages-container";
        public string BlobContainerName_CallRecords { get; } = "callrecords-container";
        public string BlobContainerName_UserEvents { get; } = "userevents-container";
        public string BlobContainerName_SubscriptionList { get; } = "subscription-container";

        public string EventHubTopic_ChatMeesages { get; } = "chatmessages-topic";
        public string EventHubTopic_CallRecords { get; } = "callrecords-topic";
        public string EventHubTopic_UserEvents { get; } = "userevents-topic";

        
        /// <summary>
        /// MSFT GraphAPI related configuration
        /// </summary>
        /// 
        
        // For application level authentication in scopes
        public string ApplicationScope { get; } = "https://graph.microsoft.com/.default";
    }
}