using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Models.Mappers;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services
{
    public class FlashcardService : IFlashcardService
    {
        private readonly INoteGraphAccessService _noteGraphAccessService;
        private readonly INoteNodeRepository _noteNodeRepository;
        private readonly IFlashcardRepository _flashcardRepository;
        private readonly TimeProvider _time;

        public FlashcardService(
            INoteGraphAccessService noteGraphAccessService,
            INoteNodeRepository noteNodeRepository,
            IFlashcardRepository flashcardRepository,
            TimeProvider time)
        {
            _noteGraphAccessService = noteGraphAccessService;
            _noteNodeRepository = noteNodeRepository;
            _flashcardRepository = flashcardRepository;
            _time = time;
        }

        public async Task<Result<GetFlashcardsResponse>> GetFlashcards(Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            var error = await CheckNode(noteGraphId, noteNodeId, ct);
            if (error is not null)
                return Result<GetFlashcardsResponse>.Fail(error);

            var cards = await _flashcardRepository.GetByNodeAsync(noteGraphId, noteNodeId, ct);
            return Result<GetFlashcardsResponse>.Ok(new GetFlashcardsResponse
            {
                Cards = cards.Select(c => c.ToFlashcardResult()).ToList()
            });
        }

        public async Task<Result<FlashcardResult>> CreateFlashcard(CreateFlashcardRequest request, Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            var error = ValidateSides(request.Front, request.Back) ?? await CheckNode(noteGraphId, noteNodeId, ct);
            if (error is not null)
                return Result<FlashcardResult>.Fail(error);

            var now = _time.GetUtcNow().UtcDateTime;
            var card = new Flashcard
            {
                Id = Guid.NewGuid(),
                GraphId = noteGraphId,
                NodeId = noteNodeId,
                Front = request.Front.Trim(),
                Back = request.Back.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };

            try
            {
                await _flashcardRepository.SaveAsync(card, ct);
                return Result<FlashcardResult>.Ok(card.ToFlashcardResult());
            }
            catch
            {
                return Result<FlashcardResult>.Fail("Failed to create flashcard.");
            }
        }

        public async Task<Result<FlashcardResult>> EditFlashcard(EditFlashcardRequest request, Guid noteGraphId, Guid noteNodeId, Guid cardId, CancellationToken ct)
        {
            var error = ValidateSides(request.Front, request.Back) ?? await CheckNode(noteGraphId, noteNodeId, ct);
            if (error is not null)
                return Result<FlashcardResult>.Fail(error);

            var card = await _flashcardRepository.GetByIdAsync(noteGraphId, cardId, ct);
            if (card is null || card.NodeId != noteNodeId)
                return Result<FlashcardResult>.Fail("Flashcard not found on this note.");

            card.Front = request.Front.Trim();
            card.Back = request.Back.Trim();
            card.UpdatedAt = _time.GetUtcNow().UtcDateTime;

            try
            {
                await _flashcardRepository.SaveAsync(card, ct);
                return Result<FlashcardResult>.Ok(card.ToFlashcardResult());
            }
            catch
            {
                return Result<FlashcardResult>.Fail("Failed to edit flashcard.");
            }
        }

        public async Task<Result<DeleteFlashcardResponse>> DeleteFlashcard(Guid noteGraphId, Guid noteNodeId, Guid cardId, CancellationToken ct)
        {
            var error = await CheckNode(noteGraphId, noteNodeId, ct);
            if (error is not null)
                return Result<DeleteFlashcardResponse>.Fail(error);

            var card = await _flashcardRepository.GetByIdAsync(noteGraphId, cardId, ct);
            if (card is null || card.NodeId != noteNodeId)
                return Result<DeleteFlashcardResponse>.Fail("Flashcard not found on this note.");

            try
            {
                await _flashcardRepository.DeleteAsync(noteGraphId, cardId, ct);
                return Result<DeleteFlashcardResponse>.Ok(new DeleteFlashcardResponse { Id = cardId, IsDeleted = true });
            }
            catch
            {
                return Result<DeleteFlashcardResponse>.Fail("Failed to delete flashcard.");
            }
        }

        // Returns why the note can't be used, or null when it belongs to the user's graph.
        private async Task<string?> CheckNode(Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return metadataResult.Error!;

            return await _noteNodeRepository.ExistsAsync(noteGraphId, noteNodeId, ct)
                ? null
                : "Node not found in graph.";
        }

        private static string? ValidateSides(string front, string back)
        {
            if (string.IsNullOrWhiteSpace(front) || string.IsNullOrWhiteSpace(back))
                return "A flashcard needs both a front and a back.";
            if (front.Trim().Length > FlashcardLimits.MaxSideLength || back.Trim().Length > FlashcardLimits.MaxSideLength)
                return $"Each side of a flashcard can be at most {FlashcardLimits.MaxSideLength} characters.";
            return null;
        }
    }
}
