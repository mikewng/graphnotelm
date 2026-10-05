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
    public class ReviewController : ControllerBase
    {
        private readonly ILogger<ReviewController> _logger;
        private readonly IReviewService _reviewService;

        public ReviewController(
            ILogger<ReviewController> logger,
            IReviewService reviewService)
        {
            _logger = logger;
            _reviewService = reviewService;
        }

        [HttpGet("id/{noteGraphId:guid}/review/summary")]
        public async Task<ActionResult<Result<ReviewSummaryResponse>>> GetSummary(Guid noteGraphId, [FromQuery] ReviewQueueRequest request, CancellationToken ct)
        {
            var response = await _reviewService.GetSummary(noteGraphId, request, ct);
            if (!response.Success || response.Value == null)
                return BadRequest(Result<ReviewSummaryResponse>.Fail(response.Error ?? "Failed to load review summary."));

            return Result<ReviewSummaryResponse>.Ok(response.Value);
        }

        [HttpGet("id/{noteGraphId:guid}/review/queue")]
        public async Task<ActionResult<Result<ReviewQueueResponse>>> GetQueue(Guid noteGraphId, [FromQuery] ReviewQueueRequest request, CancellationToken ct)
        {
            var response = await _reviewService.GetQueue(noteGraphId, request, ct);
            if (!response.Success || response.Value == null)
                return BadRequest(Result<ReviewQueueResponse>.Fail(response.Error ?? "Failed to load review queue."));

            return Result<ReviewQueueResponse>.Ok(response.Value);
        }

        [HttpPost("id/{noteGraphId:guid}/node/{nodeId:guid}/review")]
        public async Task<ActionResult<Result<SubmitReviewResponse>>> SubmitReview([FromBody] SubmitReviewRequest request, Guid noteGraphId, Guid nodeId, CancellationToken ct)
        {
            var response = await _reviewService.SubmitReview(request, noteGraphId, nodeId, ct);
            if (!response.Success || response.Value == null)
                return BadRequest(Result<SubmitReviewResponse>.Fail(response.Error ?? "Failed to submit review."));

            return Result<SubmitReviewResponse>.Ok(response.Value);
        }
    }
}
