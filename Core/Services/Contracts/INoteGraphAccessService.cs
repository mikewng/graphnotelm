using graphnotelm.Core.Models;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface INoteGraphAccessService
    {
        public Task<Result<NoteGraphMetadata>> GetAuthorizedMetadataAsync(Guid noteGraphId, CancellationToken ct);
        public Task<Result<NoteGraphDocument>> GetAuthorizedGraphDataAsync(Guid noteGraphId, CancellationToken ct);
        public Task<Result<NoteGraphDocument>> GetAuthorizedFullDocumentAsync(Guid noteGraphId, CancellationToken ct);
        // Like GetAuthorizedFullDocumentAsync but node Notes are left empty.
        // Read-only: the returned nodes must never be saved back.
        public Task<Result<NoteGraphDocument>> GetAuthorizedSkeletonDocumentAsync(Guid noteGraphId, CancellationToken ct);
    }
}
