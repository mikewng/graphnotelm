using graphnotelm.Infrastructure.Contracts;
using graphnotelm.Core.Contexts.Contracts;
using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services
{
    public class NoteGraphService: INoteGraphService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserContext _currentUser;
        private readonly INoteGraphMetadataRepository _noteGraphMetadataRepository;
        private readonly INoteGraphAccessService _noteGraphAccessService;

        public NoteGraphService(IUnitOfWork unitOfWork, ICurrentUserContext currentUser, INoteGraphMetadataRepository noteGraphMetadataRepository, INoteGraphAccessService noteGraphAccessService)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _noteGraphMetadataRepository = noteGraphMetadataRepository;
            _noteGraphAccessService = noteGraphAccessService;
        }

        public async Task<Result<GetGraphResponse>> GetNoteGraphById(Guid noteGraphId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
            {
                return Result<GetGraphResponse>.Fail(metadataResult.Error!);
            }

            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
            {
                return Result<GetGraphResponse>.Fail(graphDataResult.Error!);
            }

            var graphData = graphDataResult.Value!;
            GetGraphResponse dto = new GetGraphResponse()
            {
                Id = graphData.Id,
                Tags = graphData.Tags,
                Relationships = graphData.Relationships,
                Nodes = graphData.Nodes
            };

            return Result<GetGraphResponse>.Ok(dto);
        }

        public async Task<Result<GetGraphListResponse>> GetNoteGraphList(CancellationToken ct)
        {
            var graphMetadataList = await _noteGraphMetadataRepository.GetListByUserIdAsync(_currentUser.UserId, ct);
            if (graphMetadataList.Count == 0)
            {
                return Result<GetGraphListResponse>.Fail("No graphs associated with user ID.");
            }
            if (graphMetadataList is null)
            {
                return Result<GetGraphListResponse>.Fail("List returned as null.");
            }

            GetGraphListResponse dto = new GetGraphListResponse()
            {
                GraphList = graphMetadataList
            };
            return Result<GetGraphListResponse>.Ok(dto);
        }

        public async Task<Result<CreateGraphResponse>> CreateNoteGraph(CreateGraphRequest createGraphRequest, CancellationToken ct)
        {
            NoteGraphMetadata newGraphMetadata = new NoteGraphMetadata()
            {
                Id = Guid.NewGuid(),
                UserId = _currentUser.UserId,
                Name = createGraphRequest.Name,
                IsPublic = createGraphRequest.isPublic,
                IsDeleted = createGraphRequest.isDeleted
            };
            if (newGraphMetadata.Name == String.Empty)
            {
                return Result<CreateGraphResponse>.Fail("Failed to create: Name was empty.");
            }

            try
            {
                await _noteGraphMetadataRepository.AddAsync(newGraphMetadata, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return Result<CreateGraphResponse>.Ok(new CreateGraphResponse
                {
                    Id = newGraphMetadata.Id,
                    IsSuccess = true
                });
            }
            catch
            {
                return Result<CreateGraphResponse>.Fail("Failed to create new graph.");
            }
        }

        public async Task<Result<DeleteGraphResponse>> DeleteNoteGraphById(Guid noteGraphId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
            {
                return Result<DeleteGraphResponse>.Fail(metadataResult.Error!);
            }

            var graphMetadata = metadataResult.Value!;
            graphMetadata.IsDeleted = true;
            graphMetadata.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _noteGraphMetadataRepository.UpdateAsync(graphMetadata, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return Result<DeleteGraphResponse>.Ok(new DeleteGraphResponse
                {
                    id = graphMetadata.Id,
                    isDeleted = true
                });
            }
            catch
            {
                return Result<DeleteGraphResponse>.Fail("Failed to delete graph.");
            }
        }

    }
}
