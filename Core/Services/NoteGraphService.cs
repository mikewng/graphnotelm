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
        private readonly INoteGraphRepository _noteGraphRepository;
        private readonly INoteGraphAccessService _noteGraphAccessService;

        public NoteGraphService(
            IUnitOfWork unitOfWork, 
            ICurrentUserContext currentUser, 
            INoteGraphMetadataRepository noteGraphMetadataRepository, 
            INoteGraphRepository noteGraphRepository,
            INoteGraphAccessService noteGraphAccessService
            )
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _noteGraphMetadataRepository = noteGraphMetadataRepository;
            _noteGraphRepository = noteGraphRepository;
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
                Description = createGraphRequest.Description,
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

                NoteGraphDocument newGraphDocument = new NoteGraphDocument()
                {
                    Id = newGraphMetadata.Id,
                    UserId = _currentUser.UserId
                };

                // TODO: Save full
                await _noteGraphRepository.SaveAsync(newGraphDocument);
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

        public async Task<Result<EditGraphMetadataResponse>> EditGraphMetadataById(EditGraphMetadataRequest editGraphMetadataRequest, Guid noteGraphId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
            {
                return Result<EditGraphMetadataResponse>.Fail(metadataResult.Error!);
            }

            var graphMetadata = metadataResult.Value!;
            graphMetadata.Name = editGraphMetadataRequest.Name ?? graphMetadata.Name;
            graphMetadata.Description = editGraphMetadataRequest.Description ?? graphMetadata.Description;
            graphMetadata.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _noteGraphMetadataRepository.UpdateAsync(graphMetadata, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return Result<EditGraphMetadataResponse>.Ok(new EditGraphMetadataResponse
                {
                    Id = graphMetadata.Id,
                    Name = graphMetadata.Name,
                    Description = graphMetadata.Description
                });
            }
            catch
            {
                return Result<EditGraphMetadataResponse>.Fail("Failed to edit graph metadata.");
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

        public async Task<Result<DeleteGraphResponse>> HardDeleteNoteGraphById(Guid noteGraphId, CancellationToken ct) 
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success || metadataResult.Value == null)
            {
                return Result<DeleteGraphResponse>.Fail(metadataResult.Error!);
            }
            if (!metadataResult.Value.IsDeleted)
            {
                return Result<DeleteGraphResponse>.Fail(metadataResult.Error!);
            }

            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
            {
                return Result<DeleteGraphResponse>.Fail(graphDataResult.Error!);
            }

            try
            {
                // Hard Delete Metadata Content

                // Hard Delete Repository Content
                await _noteGraphRepository.DeleteByIdAsync(noteGraphId);
                await _unitOfWork.SaveChangesAsync(ct);
                return Result<DeleteGraphResponse>.Ok(new DeleteGraphResponse{id = metadataResult.Value.Id,isDeleted = true});
            }
            catch
            {
                return Result<DeleteGraphResponse>.Fail("Failed to hard delete graph.");
            }
        }

        public async Task<Result<CreateGraphResponse>> ImportNoteGraphFromJSON(NoteGraphDocumentREADONLY document, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(document.Name))
                return Result<CreateGraphResponse>.Fail("Failed to import: Name was empty.");

            var newMetadata = new NoteGraphMetadata
            {
                Id = Guid.NewGuid(),
                UserId = _currentUser.UserId,
                Name = document.Name,
                IsDeleted = false
            };

            try
            {
                await _noteGraphMetadataRepository.AddAsync(newMetadata, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                var newDocument = new NoteGraphDocument
                {
                    Id = newMetadata.Id,
                    UserId = _currentUser.UserId,
                    Tags = document.Tags,
                    Relationships = document.Relationships,
                    Nodes = document.Nodes
                };

                await _noteGraphRepository.SaveAsync(newDocument);

                return Result<CreateGraphResponse>.Ok(new CreateGraphResponse
                {
                    Id = newMetadata.Id,
                    IsSuccess = true
                });
            }
            catch
            {
                return Result<CreateGraphResponse>.Fail("Failed to import graph from JSON.");
            }
        }

        public async Task<Result<NoteGraphDocumentREADONLY>> ExportNoteGraphAsJSON(Guid noteGraphId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return Result<NoteGraphDocumentREADONLY>.Fail(metadataResult.Error!);

            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<NoteGraphDocumentREADONLY>.Fail(graphDataResult.Error!);

            var graphData = graphDataResult.Value!;
            var exportDoc = new NoteGraphDocumentREADONLY
            {
                Name = metadataResult.Value!.Name,
                Tags = graphData.Tags,
                Relationships = graphData.Relationships,
                Nodes = graphData.Nodes
            };

            return Result<NoteGraphDocumentREADONLY>.Ok(exportDoc);
        }

    }
}
