using graphnotelm.Core.Models;
using graphnotelm.Utils;

namespace graphnotelm.Infrastructure.Repository.Contracts
{
    public interface INoteGraphMetadataRepository
    {
        public Task AddAsync(NoteGraphMetadata noteGraphMetadata, CancellationToken ct = default);
        public Task<NoteGraphMetadata?> GetByIdAsync(Guid noteGraphId, CancellationToken ct = default);
        public Task<List<NoteGraphMetadata>> GetListByUserIdAsync(Guid userId, CancellationToken ct = default);
    }
}
