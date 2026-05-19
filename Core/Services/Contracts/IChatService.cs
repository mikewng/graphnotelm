using graphnotelm.Core.Models;
using Microsoft.Extensions.AI;

namespace graphnotelm.Core.Services.Contracts
{
    public interface IChatService
    {
        IAsyncEnumerable<AgentEvent> RunAsync(
            Guid graphId,
            IEnumerable<ChatMessage> messageHistory,
            CancellationToken ct = default);
    }
}
