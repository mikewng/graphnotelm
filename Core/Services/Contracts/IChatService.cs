using graphnotelm.Core.Models;

namespace graphnotelm.Core.Services.Contracts
{
    public interface IChatService
    {
        IAsyncEnumerable<string> StreamResponseAsync(Guid userId, Guid graphId, List<LLMChatMessage> messageHistory, CancellationToken cancellationToken = default);
    }
}
