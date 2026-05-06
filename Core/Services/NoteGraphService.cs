using card_library.Core.Application.Repository.Contracts;
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
        private readonly INoteGraphRepository _noteGraphRepository;

        public NoteGraphService(IUnitOfWork unitOfWork, ICurrentUserContext currentUser, INoteGraphMetadataRepository noteGraphMetadataRepository, INoteGraphRepository noteGraphRepository)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _noteGraphMetadataRepository = noteGraphMetadataRepository;
            _noteGraphRepository = noteGraphRepository;
        }

        public async Task<Result<GetGraphResponse>> GetNoteGraphById(Guid noteGraphId, CancellationToken ct)
        {
            // Retreive graph metadata
            var graphMetadata = await _noteGraphMetadataRepository.GetByIdAsync(noteGraphId, ct);
            if (graphMetadata is null)
            {
                return Result<GetGraphResponse>.Fail("Graph metadata not found");
            }
            if (graphMetadata.UserId != _currentUser.UserId)
            {
                return Result<GetGraphResponse>.Fail("UserId mismatch. Access to graph metadata denied.");
            }

            // Retreive full Data from DynamoDB Repo
            var graphData = await _noteGraphRepository.GetByIdAsync(noteGraphId, ct);
            if (graphData is null)
            {
                return Result<GetGraphResponse>.Fail("Full associated graph data not found");
            }
            if (graphData.UserId != _currentUser.UserId)
            {
                return Result<GetGraphResponse>.Fail("UserId mismatch. Access to full data of graph denied.");
            }

            // DTO Transform
            GetGraphResponse dto = new GetGraphResponse() { 
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
            var graphMetadata = await _noteGraphMetadataRepository.GetByIdAsync(noteGraphId, ct);
            if (graphMetadata is null)
            {
                return Result<DeleteGraphResponse>.Fail("Graph metadata not found");
            }
            if (graphMetadata.UserId != _currentUser.UserId)
            {
                return Result<DeleteGraphResponse>.Fail("UserId mismatch. Access to graph metadata denied.");
            }

            //TODO: Mark as IsDeleted = True via patching
            throw new NotImplementedException();
        }

    }
}
