using graphnotelm.Core.Contexts.Contracts;
using graphnotelm.Core.Models;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services
{
    public class NoteGraphAccessService : INoteGraphAccessService
    {
        private readonly ICurrentUserContext _currentUser;
        private readonly INoteGraphMetadataRepository _noteGraphMetadataRepository;
        private readonly INoteGraphRepository _noteGraphRepository;

        public NoteGraphAccessService(ICurrentUserContext currentUser, INoteGraphMetadataRepository noteGraphMetadataRepository, INoteGraphRepository noteGraphRepository)
        {
            _currentUser = currentUser;
            _noteGraphMetadataRepository = noteGraphMetadataRepository;
            _noteGraphRepository = noteGraphRepository;
        }

        public async Task<Result<NoteGraphMetadata>> GetAuthorizedMetadataAsync(Guid noteGraphId, CancellationToken ct)
        {
            var graphMetadata = await _noteGraphMetadataRepository.GetByIdAsync(noteGraphId, ct);
            if (graphMetadata is null)
            {
                return Result<NoteGraphMetadata>.Fail("Graph metadata not found");
            }
            if (graphMetadata.UserId != _currentUser.UserId)
            {
                return Result<NoteGraphMetadata>.Fail("UserId mismatch. Access to graph metadata denied.");
            }
            return Result<NoteGraphMetadata>.Ok(graphMetadata);
        }

        public async Task<Result<NoteGraphDocument>> GetAuthorizedGraphDataAsync(Guid noteGraphId, CancellationToken ct)
        {
            var graphData = await _noteGraphRepository.GetByIdAsync(noteGraphId, ct);
            if (graphData is null)
            {
                return Result<NoteGraphDocument>.Fail("Full associated graph data not found");
            }
            if (graphData.UserId != _currentUser.UserId)
            {
                return Result<NoteGraphDocument>.Fail("UserId mismatch. Access to full data of graph denied.");
            }
            return Result<NoteGraphDocument>.Ok(graphData);
        }
    }
}
