namespace global_class
{
    public class SubscriptionData
    {
        public List<Value> value { get; set; }
    }

    public class Value
    {
        public string tenantId { get; set; }
        public string subscriptionId { get; set; }
        public string clientState { get; set; }
        public string changeType { get; set; }
        public string resource { get; set; }
        public DateTime subscriptionExpirationDateTime { get; set; }
        public ResourceData resourceData { get; set; }
    }

    public class ResourceData
    {
        public string oDataType { get; set; }
        public string oDataId { get; set; }
        public string id { get; set; }
    }

    /// <summary>
    /// Create or Renew Subscription Class
    /// </summary>
    public class SubscriptionList
    {
        public List<SubscriptionInfo> value { get; set; }
    }

    public class SubscriptionInfo
    {
        public SubscriptionInfo(string userId, string subscriptionId)
        {
            UserId = userId;
            SubscriptionId = subscriptionId;
        }

        public string UserId { get; set; }
        public string SubscriptionId { get; set; }
    }
}
