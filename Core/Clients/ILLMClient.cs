using graphnotelm.Core.Models;

namespace graphnotelm.Core.Clients
{
    public interface ILLMClient
    {
        Task<string> CompleteAsync(string systemPrompt, string userPrompt);
        IAsyncEnumerable<string> StreamAsync(string systemPrompt, IEnumerable<LLMChatMessage> messages, CancellationToken ct = default);
    }
}
