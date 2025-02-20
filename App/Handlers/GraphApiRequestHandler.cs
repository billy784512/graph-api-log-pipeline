using System.Reflection;

using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.CallRecords;
using Microsoft.Extensions.Logging;

using App.Utils;


namespace App.Handlers
{
    public class GraphApiRequestHandler
    {
        private readonly GraphServiceClient _graphServiceClient;
        private readonly ILogger _logger;

        public GraphApiRequestHandler(GraphServiceClient graphServiceClient, ILoggerFactory loggerFactory){
            _graphServiceClient = graphServiceClient;
            _logger = loggerFactory.CreateLogger<GraphApiRequestHandler>();
        }

        public async Task<Subscription> CreateSubscription(Subscription subscription)
        {
            try
            {
                var responseSubscription = await _graphServiceClient.Subscriptions.PostAsync(subscription);
                return responseSubscription;
            }
            catch (Exception ex)
            {
                _logger.LogError(ErrorMessage.ERR_METHOD_EXECUTE, UtilityFunction.GetCurrentMethodName(), ex.Message);
                return null;
            }
        }

        public async Task RenewSubscription(string subscriptionId)
        {
            try
            {
                var subscriptionToUpdate = new Subscription
                {
                    ExpirationDateTime = DateTime.UtcNow.AddDays(2),
                };
                await _graphServiceClient.Subscriptions[subscriptionId].PatchAsync(subscriptionToUpdate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ErrorMessage.ERR_METHOD_EXECUTE, UtilityFunction.GetCurrentMethodName(), ex.Message);
            }
        }

        public async Task<UserCollectionResponse> GetUSer(){
            try
            {
                var users = await _graphServiceClient.Users.GetAsync();
                return users;
            }
            catch(Exception ex)
            {
                _logger.LogError(ErrorMessage.ERR_METHOD_EXECUTE, UtilityFunction.GetCurrentMethodName(), ex.Message);
                return null;
            }
        }

        public async Task<CallRecord> GetCallRecords(string call_Id)
        {
            try
            {
                CallRecord callrecord = await _graphServiceClient.Communications.CallRecords[call_Id]
                    .GetAsync(requestConfiguration => {
                        requestConfiguration.QueryParameters.Expand = new[] { "sessions($expand=segments)"};
                    });
                    
                return callrecord;
            }
            catch (Exception ex)
            {
                _logger.LogError(ErrorMessage.ERR_METHOD_EXECUTE, UtilityFunction.GetCurrentMethodName(), ex.Message);
                return null;
            }
        }

        public async Task<Event> GetUserEvent(string userId, string eventId)
        {
            try
            {
                var userEvent = await _graphServiceClient.Users[userId].Events[eventId].GetAsync();
                return userEvent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ErrorMessage.ERR_METHOD_EXECUTE, UtilityFunction.GetCurrentMethodName(), ex.Message);
                return null;
            }
        }

        public async Task<ChatMessageCollectionResponse> GetChatMessages(string chatId){
            try{
                var messages = await _graphServiceClient.Chats[chatId].Messages.GetAsync();
                return messages;
            }
            catch(Exception ex)
            {
                _logger.LogError("failed to fetch chat messages for chatId: {chatId}, {message}", chatId, ex.Message);
                _logger.LogError(ErrorMessage.ERR_METHOD_EXECUTE, UtilityFunction.GetCurrentMethodName(), ex.Message);
                return null;
            }
        }
    }
}