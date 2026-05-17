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
        private readonly IChatService _chatService;

        public AIChatHub(ICurrentUserContext currentUserContext, IChatService chatService)
        {
            _currentUserContext = currentUserContext;
            _chatService = chatService;
        }

        public async Task SendMessage(Guid graphId, IEnumerable<LLMChatMessage> messages)
        {
            var userId = _currentUserContext.UserId;

            var chatMessages = messages.Select(m => new ChatMessage(
                m.Role == "user" ? ChatRole.User : ChatRole.Assistant,
                m.Content)).ToList();

            try
            {
                await foreach (var evt in _chatService.RunAsync(userId, graphId, chatMessages, Context.ConnectionAborted))
                {
                    switch (evt)
                    {
                        case ContentDelta d:
                            await Clients.Caller.SendAsync("ReceiveChunk", d.Text);
                            break;
                        case ToolInvoked t:
                            await Clients.Caller.SendAsync("ReceiveToolCall", t.ToolName);
                            break;
                        case ToolResult r:
                            await Clients.Caller.SendAsync("ReceiveToolResult", r.ToolName);
                            break;
                        case TurnComplete:
                            await Clients.Caller.SendAsync("ResponseComplete");
                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnected — nothing to do
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
