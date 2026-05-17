using Microsoft.Extensions.AI;

namespace graphnotelm.Core.Services.Contracts
{
    public interface IChatService
    {
        IAsyncEnumerable<string> StreamResponseAsync(
            Guid userId,
            Guid graphId,
            IEnumerable<ChatMessage> messageHistory,
            CancellationToken ct = default);
    }
}
