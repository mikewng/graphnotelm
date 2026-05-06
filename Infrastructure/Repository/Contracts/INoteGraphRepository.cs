using graphnotelm.Core.Models;

namespace graphnotelm.Infrastructure.Repository.Contracts
{
    public interface INoteGraphRepository
    {
        public Task<NoteGraphDocument?> GetByIdAsync(Guid noteGraphId, CancellationToken ct = default);
    }
}
