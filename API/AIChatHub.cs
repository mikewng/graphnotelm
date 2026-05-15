using graphnotelm.Core.Contexts.Contracts;
using graphnotelm.Core.Services;
using Microsoft.AspNetCore.SignalR;

namespace graphnotelm.API
{
    public class AIChatHub : Hub
    {

        private readonly ICurrentUserContext _currentUserContext;
        private readonly IChatService _chatService;
        public AIChatHub(ICurrentUserContext currentUserContext, IChatService chatService)
        {
            _currentUserContext = currentUserContext;
            _chatService = chatService;
        }
        public async Task SendMessage(Guid graphId, string message)
        {
            var userId = _currentUserContext.UserId;

            await foreach (var chunk in _chatService.StreamResponseAsync(userId, graphId, message) {
                await Clients.Caller.SendAsync("ReceiveChunk", chunk);
            }

            await Clients.Caller.SendAsync("ResponseComplete");
        }
    }
}
