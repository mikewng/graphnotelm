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
    public class NoteGraphAnalysisController : ControllerBase
    {
        private readonly ILogger<NoteGraphAnalysisController> _logger;
        private readonly IGraphAnalysisService _graphAnalysisService;

        public NoteGraphAnalysisController(
            ILogger<NoteGraphAnalysisController> logger,
            IGraphAnalysisService graphAnalysisService)
        {
            _logger = logger;
            _graphAnalysisService = graphAnalysisService;
        }

        [HttpGet("id/{noteGraphId:guid}/analysis/weakestpath")]
        public async Task<ActionResult<Result<WeakestPathResponse>>> GetWeakestPath(Guid noteGraphId, [FromQuery] WeakestPathRequest request, CancellationToken ct)
        {
            var response = await _graphAnalysisService.FindWeakestPath(noteGraphId, request, ct);
            if (!response.Success || response.Value == null)
                return BadRequest(Result<WeakestPathResponse>.Fail(response.Error ?? "Failed to find weakest path."));

            return Result<WeakestPathResponse>.Ok(response.Value);
        }

        [HttpGet("id/{noteGraphId:guid}/analysis/frontier")]
        public async Task<ActionResult<Result<KnowledgeFrontierResponse>>> GetKnowledgeFrontier(Guid noteGraphId, [FromQuery] KnowledgeFrontierRequest request, CancellationToken ct)
        {
            var response = await _graphAnalysisService.FindKnowledgeFrontier(noteGraphId, request, ct);
            if (!response.Success || response.Value == null)
                return BadRequest(Result<KnowledgeFrontierResponse>.Fail(response.Error ?? "Failed to find knowledge frontier."));

            return Result<KnowledgeFrontierResponse>.Ok(response.Value);
        }

        [HttpGet("id/{noteGraphId:guid}/analysis/learningorder")]
        public async Task<ActionResult<Result<LearningOrderResponse>>> GetLearningOrder(Guid noteGraphId, [FromQuery] LearningOrderRequest request, CancellationToken ct)
        {
            var response = await _graphAnalysisService.FindLearningOrder(noteGraphId, request, ct);
            if (!response.Success || response.Value == null)
                return BadRequest(Result<LearningOrderResponse>.Fail(response.Error ?? "Failed to find learning order."));

            return Result<LearningOrderResponse>.Ok(response.Value);
        }

        [HttpGet("id/{noteGraphId:guid}/analysis/readytolearn")]
        public async Task<ActionResult<Result<ReadyToLearnResponse>>> GetReadyToLearn(Guid noteGraphId, [FromQuery] ReadyToLearnRequest request, CancellationToken ct)
        {
            var response = await _graphAnalysisService.FindReadyToLearn(noteGraphId, request, ct);
            if (!response.Success || response.Value == null)
                return BadRequest(Result<ReadyToLearnResponse>.Fail(response.Error ?? "Failed to find notes ready to learn."));

            return Result<ReadyToLearnResponse>.Ok(response.Value);
        }

        [HttpGet("id/{noteGraphId:guid}/analysis/bottlenecks")]
        public async Task<ActionResult<Result<BottlenecksResponse>>> GetBottlenecks(Guid noteGraphId, [FromQuery] BottlenecksRequest request, CancellationToken ct)
        {
            var response = await _graphAnalysisService.FindBottlenecks(noteGraphId, request, ct);
            if (!response.Success || response.Value == null)
                return BadRequest(Result<BottlenecksResponse>.Fail(response.Error ?? "Failed to find bottlenecks."));

            return Result<BottlenecksResponse>.Ok(response.Value);
        }
    }
}
