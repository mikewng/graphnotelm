using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Models.Mappers;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Core.Utils;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services
{
    public class ReviewService : IReviewService
    {
        private readonly INoteGraphAccessService _noteGraphAccessService;
        private readonly INoteNodeRepository _noteNodeRepository;
        private readonly IFlashcardRepository _flashcardRepository;
        private readonly IReviewLogRepository _reviewLogRepository;
        private readonly TimeProvider _time;

        public ReviewService(
            INoteGraphAccessService noteGraphAccessService,
            INoteNodeRepository noteNodeRepository,
            IFlashcardRepository flashcardRepository,
            IReviewLogRepository reviewLogRepository,
            TimeProvider time)
        {
            _noteGraphAccessService = noteGraphAccessService;
            _noteNodeRepository = noteNodeRepository;
            _flashcardRepository = flashcardRepository;
            _reviewLogRepository = reviewLogRepository;
            _time = time;
        }

        private DateTime Now => _time.GetUtcNow().UtcDateTime;

        public async Task<Result<ReviewSummaryResponse>> GetSummary(Guid noteGraphId, ReviewQueueRequest request, CancellationToken ct)
        {
            var queueResult = await GetQueue(noteGraphId, request, ct);
            if (!queueResult.Success)
                return Result<ReviewSummaryResponse>.Fail(queueResult.Error!);

            return Result<ReviewSummaryResponse>.Ok(new ReviewSummaryResponse
            {
                DueCount = queueResult.Value!.DueCount,
                NewCount = queueResult.Value.NewCount
            });
        }

        public async Task<Result<ReviewQueueResponse>> GetQueue(Guid noteGraphId, ReviewQueueRequest request, CancellationToken ct)
        {
            // Scheduling only needs titles and memory state, so skip loading note bodies.
            var documentResult = await _noteGraphAccessService.GetAuthorizedSkeletonDocumentAsync(noteGraphId, ct);
            if (!documentResult.Success)
                return Result<ReviewQueueResponse>.Fail(documentResult.Error!);

            var now = Now;
            var nodes = documentResult.Value!.Nodes.Values;
            var cardsByNode = (await _flashcardRepository.GetByGraphAsync(noteGraphId, ct))
                .GroupBy(c => c.NodeId)
                .ToDictionary(g => g.Key, g => g.Select(c => c.ToFlashcardResult()).ToList());

            var due = nodes
                .Where(n => n.Metadata.Memory is { } memory && memory.DueAt <= now)
                .OrderBy(n => n.Metadata.Memory!.DueAt)
                .ToList();

            // A note joins the queue on its own once it has cards; until then it's reviewed on demand.
            // Lower self-ratings come first, so notes the user already rates highly are introduced later.
            var newLimit = Math.Clamp(request.NewLimit, 0, FlashcardLimits.MaxNewPerSession);
            var fresh = nodes
                .Where(n => n.Metadata.Memory is null && cardsByNode.ContainsKey(n.Id))
                .OrderBy(n => n.Metadata.UserConfidenceRate)
                .ThenBy(n => n.Title, StringComparer.OrdinalIgnoreCase)
                .Take(newLimit)
                .ToList();

            ReviewQueueItem ToItem(NoteNode node) => new()
            {
                NodeId = node.Id,
                Title = node.Title,
                IsNew = node.Metadata.Memory is null,
                DueAt = node.Metadata.Memory?.DueAt,
                Confidence = MemoryModel.Confidence(node.Metadata, now),
                Cards = cardsByNode.GetValueOrDefault(node.Id) ?? new()
            };

            return Result<ReviewQueueResponse>.Ok(new ReviewQueueResponse
            {
                Items = due.Concat(fresh).Select(ToItem).ToList(),
                DueCount = due.Count,
                NewCount = fresh.Count
            });
        }

        public async Task<Result<SubmitReviewResponse>> SubmitReview(SubmitReviewRequest request, Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            if (!Enum.IsDefined(request.Grade))
                return Result<SubmitReviewResponse>.Fail("Grade must be 1 (Again), 2 (Hard), 3 (Good) or 4 (Easy).");

            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return Result<SubmitReviewResponse>.Fail(metadataResult.Error!);

            // A full node, since it is saved back.
            var node = await _noteNodeRepository.GetByIdAsync(noteGraphId, noteNodeId, ct);
            if (node is null)
                return Result<SubmitReviewResponse>.Fail("Node not found in graph.");

            if (request.CardId is { } cardId)
            {
                var card = await _flashcardRepository.GetByIdAsync(noteGraphId, cardId, ct);
                if (card is null || card.NodeId != noteNodeId)
                    return Result<SubmitReviewResponse>.Fail("Flashcard not found on this note.");
            }

            var now = Now;
            var prior = node.Metadata.Memory;
            var memory = MemoryModel.Review(prior, request.Grade, now);
            node.Metadata.Memory = memory;

            try
            {
                await _noteNodeRepository.SaveAsync(noteGraphId, node);
            }
            catch
            {
                return Result<SubmitReviewResponse>.Fail("Failed to save review.");
            }

            // Best-effort: the review already counts; a missing log entry only loses history.
            try
            {
                await _reviewLogRepository.AddAsync(new ReviewLogEntry
                {
                    Id = Guid.NewGuid(),
                    GraphId = noteGraphId,
                    NodeId = noteNodeId,
                    CardId = request.CardId,
                    Grade = request.Grade,
                    ReviewedAt = now,
                    ElapsedDays = prior is null ? 0 : Math.Max(0, (now - prior.LastReviewedAt).TotalDays),
                    Retrievability = prior is null ? null : MemoryModel.Retrievability(prior, now),
                    StabilityAfter = memory.Stability,
                    DifficultyAfter = memory.Difficulty
                }, ct);
            }
            catch { }

            return Result<SubmitReviewResponse>.Ok(new SubmitReviewResponse
            {
                NodeId = noteNodeId,
                Confidence = MemoryModel.Confidence(node.Metadata, now),
                Memory = memory
            });
        }
    }
}
