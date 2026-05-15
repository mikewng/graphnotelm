namespace graphnotelm.Core.Services
{
    public interface IChatService
    {
        IAsyncEnumerable<string> StreamResponseAsync(Guid userId, Guid graphId, string message, CancellationToken cancellationToken = default);
    }
}
