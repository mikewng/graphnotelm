using graphnotelm.Core.Models;

namespace graphnotelm.Infrastructure.Repository.Contracts
{
    public interface INoteGraphRepository
    {
        public Task<NoteGraphDocument?> GetByIdAsync(Guid noteGraphId, CancellationToken ct = default);
        public Task SaveAsync(NoteGraphDocument document);
        public Task DeleteByIdAsync(Guid noteGraphId);
    }
}
