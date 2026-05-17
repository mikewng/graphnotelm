using graphnotelm.Core.Contexts.Contracts;
using graphnotelm.Core.Models;
using graphnotelm.Core.Services.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;

namespace graphnotelm.API
{
    [Authorize]
    public class AIChatHub : Hub
    {

        private readonly ICurrentUserContext _currentUserContext;
        private readonly IChatClient _chatClient;
        public AIChatHub(ICurrentUserContext currentUserContext, IChatClient chatClient)
        {
            _currentUserContext = currentUserContext;
            _chatClient = chatClient;
        }
        public async Task SendMessage(Guid graphId, IEnumerable<LLMChatMessage> messages)
        {
            var userId = _currentUserContext.UserId;

            var chatMessages = messages.Select(m => new ChatMessage(
                m.Role == "user" ? ChatRole.User : ChatRole.Assistant,
                m.Content)).ToList();

            try
            {
                await foreach (var update in _chatClient.GetStreamingResponseAsync(chatMessages, cancellationToken: new CancellationToken()))
                {
                    if (update.Text is not null)
                        await Clients.Caller.SendAsync("ReceiveChunk", update.Text);
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
