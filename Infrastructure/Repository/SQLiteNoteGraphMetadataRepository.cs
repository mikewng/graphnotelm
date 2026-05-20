using graphnotelm.Core.Models;
using graphnotelm.Infrastructure.Repositories;
using graphnotelm.Infrastructure.Repository.Contracts;

namespace graphnotelm.Infrastructure.Repository
{
    public class SQLiteNoteGraphMetadataRepository : Repository<NoteGraphMetadata>, INoteGraphMetadataRepository
    {
        public SQLiteNoteGraphMetadataRepository(AppDbContext context) : base(context) { }

        public Task AddAsync(NoteGraphMetadata noteGraphMetadata, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<NoteGraphMetadata?> GetByIdAsync(Guid noteGraphId, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<List<NoteGraphMetadata>> GetListByUserIdAsync(Guid userId, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateAsync(NoteGraphMetadata noteGraphMetadata, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}
