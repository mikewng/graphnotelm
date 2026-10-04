using graphnotelm.Core.Models;

namespace graphnotelm.Infrastructure.Repository.Contracts
{
    // Append-only history of reviews — kept to tune the scheduler and show progress later.
    public interface IReviewLogRepository
    {
        public Task AddAsync(ReviewLogEntry entry, CancellationToken ct = default);
        public Task<List<ReviewLogEntry>> GetByNodeAsync(Guid noteGraphId, Guid noteNodeId, CancellationToken ct = default);
        public Task DeleteByNodeAsync(Guid noteGraphId, Guid noteNodeId, CancellationToken ct = default);
        public Task DeleteByGraphAsync(Guid noteGraphId, CancellationToken ct = default);
    }
}
