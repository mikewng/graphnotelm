using graphnotelm.Core.Models;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface INoteGraphAccessService
    {
        public Task<Result<NoteGraphMetadata>> GetAuthorizedMetadataAsync(Guid noteGraphId, CancellationToken ct);
        public Task<Result<NoteGraphDocument>> GetAuthorizedGraphDataAsync(Guid noteGraphId, CancellationToken ct);
    }
}
