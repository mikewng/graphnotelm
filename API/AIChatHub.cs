using graphnotelm.Core.Contexts.Contracts;
using graphnotelm.Core.Models;
using graphnotelm.Core.Services.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace graphnotelm.API
{
    [Authorize]
    public class AIChatHub : Hub
    {

        private readonly ICurrentUserContext _currentUserContext;
        private readonly IChatService _chatService;
        public AIChatHub(ICurrentUserContext currentUserContext, IChatService chatService)
        {
            _currentUserContext = currentUserContext;
            _chatService = chatService;
        }
        public async Task SendMessage(Guid graphId, List<LLMChatMessage> messages)
        {
            var userId = _currentUserContext.UserId;

            try
            {
                await foreach (var chunk in _chatService.StreamResponseAsync(userId, graphId, messages))
                {
                    await Clients.Caller.SendAsync("ReceiveChunk", chunk);
                }
                await Clients.Caller.SendAsync("ResponseComplete");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                await Clients.Caller.SendAsync("ReceiveError", "Rate limited — please wait a moment before sending another message.");
            }
            catch (HttpRequestException ex)
            {
                await Clients.Caller.SendAsync("ReceiveError", $"AI provider error ({(int?)ex.StatusCode}). Please try again.");
            }
        }
    }
}
