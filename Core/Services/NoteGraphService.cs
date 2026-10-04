using graphnotelm.Infrastructure.Contracts;
using graphnotelm.Core.Contexts.Contracts;
using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Models.Mappers;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Core.Utils;
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
        private readonly IImageRepository _imageRepository;
        private readonly IFlashcardRepository _flashcardRepository;
        private readonly IReviewLogRepository _reviewLogRepository;
        private readonly TimeProvider _time;

        public NoteGraphService(
            IUnitOfWork unitOfWork,
            ICurrentUserContext currentUser,
            INoteGraphMetadataRepository noteGraphMetadataRepository,
            INoteGraphRepository noteGraphRepository,
            INoteGraphAccessService noteGraphAccessService,
            INoteNodeRepository noteNodeRepository,
            ILLMAnalysisService llmAnalysisService,
            IImageRepository imageRepository,
            IFlashcardRepository flashcardRepository,
            IReviewLogRepository reviewLogRepository,
            TimeProvider time
            )
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _noteGraphMetadataRepository = noteGraphMetadataRepository;
            _noteGraphRepository = noteGraphRepository;
            _noteGraphAccessService = noteGraphAccessService;
            _noteNodeRepository = noteNodeRepository;
            _llmAnalysisService = llmAnalysisService;
            _imageRepository = imageRepository;
            _flashcardRepository = flashcardRepository;
            _reviewLogRepository = reviewLogRepository;
            _time = time;
        }

        public async Task<Result<GetGraphSkeletonResponse>> GetNoteGraphById(Guid noteGraphId, CancellationToken ct)
        {
            // Skeleton response never includes note bodies, so don't load them.
            var graphDataResult = await _noteGraphAccessService.GetAuthorizedSkeletonDocumentAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<GetGraphSkeletonResponse>.Fail(graphDataResult.Error!);

            var document = graphDataResult.Value!;
            var response = document.ToGetGraphSkeletonResponse();
            var now = _time.GetUtcNow().UtcDateTime;
            foreach (var (nodeId, skeleton) in response.Nodes)
                skeleton.Confidence = MemoryModel.Confidence(document.Nodes[nodeId].Metadata, now);

            return Result<GetGraphSkeletonResponse>.Ok(response);
        }

        public async Task<Result<GetGraphListResponse>> GetNoteGraphList(CancellationToken ct)
        {
            var graphMetadataList = await _noteGraphMetadataRepository.GetListByUserIdAsync(_currentUser.UserId, ct);
            if (graphMetadataList is null)
            {
                return Result<GetGraphListResponse>.Fail("List returned as null.");
            }
            if (graphMetadataList.Count == 0)
            {
                return Result<GetGraphListResponse>.Fail("No graphs associated with user ID.");
            }

            return Result<GetGraphListResponse>.Ok(graphMetadataList.ToGetGraphListResponse());
        }

        public async Task<Result<GetGraphListResponse>> GetArchivedNoteGraphList(CancellationToken ct)
        {
            var graphMetadataList = await _noteGraphMetadataRepository.GetDeletedListByUserIdAsync(_currentUser.UserId, ct);
            if (graphMetadataList is null)
                return Result<GetGraphListResponse>.Fail("List returned as null.");

            return Result<GetGraphListResponse>.Ok(graphMetadataList.ToGetGraphListResponse());
        }

        public async Task<Result<CreateGraphResponse>> CreateNoteGraph(CreateGraphRequest createGraphRequest, CancellationToken ct)
        {
            NoteGraphMetadata newGraphMetadata = createGraphRequest.ToNoteGraphMetadata(Guid.NewGuid(), _currentUser.UserId);
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
                return Result<EditGraphMetadataResponse>.Ok(graphMetadata.ToEditGraphMetadataResponse());
            }
            catch
            {
                return Result<EditGraphMetadataResponse>.Fail("Failed to edit graph metadata.");
            }
        }

        public async Task<Result<bool>> EditGraphContextById(EditGraphContextRequest request, Guid noteGraphId, CancellationToken ct)
        {
            // Only the document (context) is touched — nodes are persisted separately.
            var accessResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
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
                return Result<DeleteGraphResponse>.Ok(graphMetadata.ToDeleteGraphResponse());
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
            }
            catch
            {
                return Result<DeleteGraphResponse>.Fail("Failed to hard delete graph.");
            }

            // Best-effort: the graph is already gone; leftover images only waste disk space,
            // and leftover cards and review history are unreachable without it.
            try
            {
                await _imageRepository.DeleteByGraphAsync(noteGraphId, ct);
            }
            catch { }

            try
            {
                await _flashcardRepository.DeleteByGraphAsync(noteGraphId, ct);
                await _reviewLogRepository.DeleteByGraphAsync(noteGraphId, ct);
            }
            catch { }

            return Result<DeleteGraphResponse>.Ok(metadata.ToDeleteGraphResponse());
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
                return Result<DeleteGraphResponse>.Ok(metadata.ToDeleteGraphResponse());
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

            var newMetadata = document.ToNoteGraphMetadata(Guid.NewGuid(), _currentUser.UserId);

            try
            {
                await _noteGraphMetadataRepository.AddAsync(newMetadata, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                var newDocument = document.ToNoteGraphDocument(newMetadata.Id, _currentUser.UserId);

                await _noteGraphRepository.SaveAsync(newDocument);
                foreach (var node in document.Nodes.Values)
                    await _noteNodeRepository.SaveAsync(newMetadata.Id, node);

                // Node ids are kept within the new graph, but cards get fresh ids so the same
                // file can be imported more than once.
                var nodeIds = document.Nodes.Values.Select(n => n.Id).ToHashSet();
                var now = _time.GetUtcNow().UtcDateTime;
                var cards = document.Flashcards
                    .Where(c => nodeIds.Contains(c.NodeId) && !string.IsNullOrWhiteSpace(c.Front) && !string.IsNullOrWhiteSpace(c.Back))
                    .Select(c => new Flashcard
                    {
                        Id = Guid.NewGuid(),
                        GraphId = newMetadata.Id,
                        NodeId = c.NodeId,
                        Front = c.Front,
                        Back = c.Back,
                        CreatedAt = now,
                        UpdatedAt = now
                    })
                    .ToList();
                if (cards.Count > 0)
                    await _flashcardRepository.SaveManyAsync(cards, ct);

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
            var nodeIds = nodes.Select(n => n.Id).ToHashSet();
            var cards = await _flashcardRepository.GetByGraphAsync(noteGraphId, ct);
            var exportDoc = new NoteGraphDocumentREADONLY
            {
                Name = metadataResult.Value!.Name,
                Tags = graphData.Tags,
                Folders = graphData.Folders,
                Relationships = graphData.Relationships,
                Nodes = nodes.ToDictionary(n => n.Id),
                Flashcards = cards.Where(c => nodeIds.Contains(c.NodeId)).Select(c => c.ToFlashcardDefinition()).ToList()
            };

            return Result<NoteGraphDocumentREADONLY>.Ok(exportDoc);
        }

    }
}
