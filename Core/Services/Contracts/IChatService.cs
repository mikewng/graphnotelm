namespace graphnotelm.Core.Services.Contracts
{
    public interface IChatService
    {
        IAsyncEnumerable<string> StreamResponseAsync(Guid userId, Guid graphId, string message, CancellationToken cancellationToken = default);
    }
}
