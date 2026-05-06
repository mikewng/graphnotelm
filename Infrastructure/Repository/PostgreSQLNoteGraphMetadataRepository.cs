using graphnotelm.Core.Models;
using graphnotelm.Infrastructure.Repositories;
using graphnotelm.Infrastructure.Repository.Contracts;
using Microsoft.EntityFrameworkCore;

namespace graphnotelm.Infrastructure.Repository
{
    public class PostgreSQLNoteGraphMetadataRepository : Repository<NoteGraphMetadata>, INoteGraphMetadataRepository
    {
        public PostgreSQLNoteGraphMetadataRepository(AppDbContext context) : base(context) { }

        public async Task AddAsync(NoteGraphMetadata noteGraphMetadata, CancellationToken ct = default)
        {
            await DbSet.AddAsync(noteGraphMetadata, ct);
        }

        public async Task<NoteGraphMetadata?> GetByIdAsync(Guid noteGraphId, CancellationToken ct = default)
        {
            return await DbSet.FirstOrDefaultAsync(m => m.Id == noteGraphId && !m.IsDeleted, ct);
        }

        public async Task<List<NoteGraphMetadata>> GetListByUserIdAsync(Guid userId, CancellationToken ct = default)
        {
            return await DbSet.Where(m => m.UserId == userId && !m.IsDeleted).OrderByDescending(m => m.UpdatedAt).ToListAsync();
        }
    }
}
