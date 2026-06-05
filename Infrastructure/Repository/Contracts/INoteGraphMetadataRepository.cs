using graphnotelm.Core.Models;
using graphnotelm.Utils;

namespace graphnotelm.Infrastructure.Repository.Contracts
{
    public interface INoteGraphMetadataRepository
    {
        public Task AddAsync(NoteGraphMetadata noteGraphMetadata, CancellationToken ct = default);
        public Task<NoteGraphMetadata?> GetByIdAsync(Guid noteGraphId, CancellationToken ct = default);
        public Task<NoteGraphMetadata?> GetDeletedByIdAsync(Guid noteGraphId, CancellationToken ct = default);
        public Task<List<NoteGraphMetadata>> GetAllAsync(CancellationToken ct = default);
        public Task<List<NoteGraphMetadata>> GetListByUserIdAsync(Guid userId, CancellationToken ct = default);
        public Task<List<NoteGraphMetadata>> GetDeletedListByUserIdAsync(Guid userId, CancellationToken ct = default);
        public Task<bool> UpdateAsync(NoteGraphMetadata noteGraphMetadata, CancellationToken ct = default);
        public Task<bool> DeleteAsync(Guid noteGraphId, CancellationToken ct = default);
    }
}
