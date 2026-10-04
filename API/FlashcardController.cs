using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace graphnotelm.API
{
    [Authorize]
    [ApiController]
    [Route("NoteGraph")]
    public class FlashcardController : ControllerBase
    {
        private readonly ILogger<FlashcardController> _logger;
        private readonly IFlashcardService _flashcardService;

        public FlashcardController(
            ILogger<FlashcardController> logger,
            IFlashcardService flashcardService)
        {
            _logger = logger;
            _flashcardService = flashcardService;
        }

        [HttpGet("id/{noteGraphId:guid}/node/{nodeId:guid}/flashcards")]
        public async Task<ActionResult<Result<GetFlashcardsResponse>>> GetFlashcards(Guid noteGraphId, Guid nodeId, CancellationToken ct)
        {
            var response = await _flashcardService.GetFlashcards(noteGraphId, nodeId, ct);
            if (!response.Success || response.Value == null)
                return BadRequest(Result<GetFlashcardsResponse>.Fail(response.Error ?? "Failed to retrieve flashcards."));

            return Result<GetFlashcardsResponse>.Ok(response.Value);
        }

        [HttpPost("id/{noteGraphId:guid}/node/{nodeId:guid}/flashcards/create")]
        public async Task<ActionResult<Result<FlashcardResult>>> CreateFlashcard([FromBody] CreateFlashcardRequest request, Guid noteGraphId, Guid nodeId, CancellationToken ct)
        {
            var response = await _flashcardService.CreateFlashcard(request, noteGraphId, nodeId, ct);
            if (!response.Success || response.Value == null)
                return BadRequest(Result<FlashcardResult>.Fail(response.Error ?? "Failed to create flashcard."));

            return Result<FlashcardResult>.Ok(response.Value);
        }

        [HttpPatch("id/{noteGraphId:guid}/node/{nodeId:guid}/flashcards/edit/{cardId:guid}")]
        public async Task<ActionResult<Result<FlashcardResult>>> EditFlashcard([FromBody] EditFlashcardRequest request, Guid noteGraphId, Guid nodeId, Guid cardId, CancellationToken ct)
        {
            var response = await _flashcardService.EditFlashcard(request, noteGraphId, nodeId, cardId, ct);
            if (!response.Success || response.Value == null)
                return BadRequest(Result<FlashcardResult>.Fail(response.Error ?? "Failed to edit flashcard."));

            return Result<FlashcardResult>.Ok(response.Value);
        }

        [HttpDelete("id/{noteGraphId:guid}/node/{nodeId:guid}/flashcards/delete/{cardId:guid}")]
        public async Task<ActionResult<Result<DeleteFlashcardResponse>>> DeleteFlashcard(Guid noteGraphId, Guid nodeId, Guid cardId, CancellationToken ct)
        {
            var response = await _flashcardService.DeleteFlashcard(noteGraphId, nodeId, cardId, ct);
            if (!response.Success || response.Value == null)
                return BadRequest(Result<DeleteFlashcardResponse>.Fail(response.Error ?? "Failed to delete flashcard."));

            return Result<DeleteFlashcardResponse>.Ok(response.Value);
        }
    }
}
