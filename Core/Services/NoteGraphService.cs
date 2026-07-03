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
        private readonly INoteNodeRepository _noteNodeRepository;
        private readonly ILLMAnalysisService _llmAnalysisService;

        public NoteGraphService(
            IUnitOfWork unitOfWork,
            ICurrentUserContext currentUser,
            INoteGraphMetadataRepository noteGraphMetadataRepository,
            INoteGraphRepository noteGraphRepository,
            INoteGraphAccessService noteGraphAccessService,
            INoteNodeRepository noteNodeRepository,
            ILLMAnalysisService llmAnalysisService
            )
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _noteGraphMetadataRepository = noteGraphMetadataRepository;
            _noteGraphRepository = noteGraphRepository;
            _noteGraphAccessService = noteGraphAccessService;
            _noteNodeRepository = noteNodeRepository;
            _llmAnalysisService = llmAnalysisService;
        }

        public async Task<Result<GetGraphSkeletonResponse>> GetNoteGraphById(Guid noteGraphId, CancellationToken ct)
        {
            var graphDataResult = await _noteGraphAccessService.GetAuthorizedFullDocumentAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<GetGraphSkeletonResponse>.Fail(graphDataResult.Error!);

            var graphData = graphDataResult.Value!;
            return Result<GetGraphSkeletonResponse>.Ok(new GetGraphSkeletonResponse
            {
                Id = graphData.Id,
                SystemPrompt = graphData.Context.SystemPrompt,
                Tags = graphData.Tags,
                Folders = graphData.Folders,
                Relationships = graphData.Relationships,
                Nodes = graphData.Nodes.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new NodeSkeleton
                    {
                        Id = kvp.Value.Id,
                        Title = kvp.Value.Title,
                        Metadata = new NodeSkeletonMetadata
                        {
                            UserConfidenceRate = kvp.Value.Metadata.UserConfidenceRate,
                            IsPinned = kvp.Value.Metadata.IsPinned
                        },
                        Relationships = kvp.Value.Relationships,
                        Tags = kvp.Value.Tags,
                        FolderId = kvp.Value.FolderId
                    })
            });
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

        public async Task<Result<GetGraphListResponse>> GetArchivedNoteGraphList(CancellationToken ct)
        {
            var graphMetadataList = await _noteGraphMetadataRepository.GetDeletedListByUserIdAsync(_currentUser.UserId, ct);
            if (graphMetadataList is null)
                return Result<GetGraphListResponse>.Fail("List returned as null.");

            return Result<GetGraphListResponse>.Ok(new GetGraphListResponse { GraphList = graphMetadataList });
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

        public async Task<Result<bool>> EditGraphContextById(EditGraphContextRequest request, Guid noteGraphId, CancellationToken ct)
        {
            var accessResult = await _noteGraphAccessService.GetAuthorizedFullDocumentAsync(noteGraphId, ct);
            if (!accessResult.Success)
                return Result<bool>.Fail(accessResult.Error!);

            var document = accessResult.Value!;
            document.Context.SystemPrompt = request.SystemPrompt;

            await _noteGraphRepository.SaveAsync(document);
            return Result<bool>.Ok(true);
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
            var metadata = await _noteGraphMetadataRepository.GetDeletedByIdAsync(noteGraphId, ct);
            if (metadata is null)
                return Result<DeleteGraphResponse>.Fail("Archived graph not found.");
            if (metadata.UserId != _currentUser.UserId)
                return Result<DeleteGraphResponse>.Fail("Access denied.");

            try
            {
                var nodes = await _noteNodeRepository.GetAllByGraphIdAsync(noteGraphId, ct);
                foreach (var node in nodes)
                    await _noteNodeRepository.DeleteAsync(noteGraphId, node.Id);

                await _noteGraphRepository.DeleteByIdAsync(noteGraphId);
                await _noteGraphMetadataRepository.DeleteAsync(noteGraphId, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                return Result<DeleteGraphResponse>.Ok(new DeleteGraphResponse { id = noteGraphId, isDeleted = true });
            }
            catch
            {
                return Result<DeleteGraphResponse>.Fail("Failed to hard delete graph.");
            }
        }

        public async Task<Result<DeleteGraphResponse>> UnarchiveNoteGraphById(Guid noteGraphId, CancellationToken ct)
        {
            var metadata = await _noteGraphMetadataRepository.GetDeletedByIdAsync(noteGraphId, ct);
            if (metadata is null)
                return Result<DeleteGraphResponse>.Fail("Archived graph not found.");
            if (metadata.UserId != _currentUser.UserId)
                return Result<DeleteGraphResponse>.Fail("Access denied.");

            metadata.IsDeleted = false;
            metadata.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _noteGraphMetadataRepository.UpdateAsync(metadata, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return Result<DeleteGraphResponse>.Ok(new DeleteGraphResponse { id = metadata.Id, isDeleted = false });
            }
            catch
            {
                return Result<DeleteGraphResponse>.Fail("Failed to unarchive graph.");
            }
        }

        public async Task<Result<CreateGraphResponse>> CreateNoteGraphFromText(CreateGraphFromTextRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
                return Result<CreateGraphResponse>.Fail("Content was empty.");

            var extractResult = await _llmAnalysisService.ExtractGraphFromTextAsync(request.Name, request.Content, ct);
            if (!extractResult.Success || extractResult.Value == null)
                return Result<CreateGraphResponse>.Fail(extractResult.Error!);

            return await ImportNoteGraphFromJSON(extractResult.Value, ct);
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
                    Folders = document.Folders,
                    Relationships = document.Relationships
                };

                await _noteGraphRepository.SaveAsync(newDocument);
                foreach (var node in document.Nodes.Values)
                    await _noteNodeRepository.SaveAsync(newMetadata.Id, node);

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
            var nodes = await _noteNodeRepository.GetAllByGraphIdAsync(noteGraphId, ct);
            var exportDoc = new NoteGraphDocumentREADONLY
            {
                Name = metadataResult.Value!.Name,
                Tags = graphData.Tags,
                Folders = graphData.Folders,
                Relationships = graphData.Relationships,
                Nodes = nodes.ToDictionary(n => n.Id)
            };

            return Result<NoteGraphDocumentREADONLY>.Ok(exportDoc);
        }

    }
}
