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
    public class NoteNodeController : ControllerBase
    {
        private readonly ILogger<NoteNodeController> _logger;
        private readonly INoteNodeService _noteNodeService;
        private readonly ILLMAnalysisService _llmAnalysisService;


        public NoteNodeController(
            ILogger<NoteNodeController> logger,
            INoteNodeService noteNodeService,
            ILLMAnalysisService llmAnalysisService)
        {
            _logger = logger;
            _noteNodeService = noteNodeService;
            _llmAnalysisService = llmAnalysisService;
        }

        [HttpGet("id/{noteGraphId:guid}/node/{noteNodeId:guid}")]
        public async Task<ActionResult<Result<GetNodeResponse>>> GetNode(Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            var getNodeResponse = await _noteNodeService.GetNodeByIds(noteGraphId, noteNodeId, ct);
            if (!getNodeResponse.Success || getNodeResponse.Value == null)
                return NotFound(Result<GetNodeResponse>.Fail("Node not found."));

            return Result<GetNodeResponse>.Ok(getNodeResponse.Value);
        }

        [HttpGet("id/{noteGraphId:guid}/nodes/search")]
        public async Task<ActionResult<Result<SearchNodesResponse>>> SearchNodes(Guid noteGraphId, [FromQuery] string q, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return BadRequest(Result<SearchNodesResponse>.Fail("Query must be at least 2 characters."));

            var searchResponse = await _noteNodeService.SearchNodesByContent(noteGraphId, q, ct);
            if (!searchResponse.Success || searchResponse.Value == null)
                return BadRequest(Result<SearchNodesResponse>.Fail("Search failed."));

            return Result<SearchNodesResponse>.Ok(searchResponse.Value);
        }

        [HttpPost("id/{noteGraphId:guid}/node/batch")]
        public async Task<ActionResult<Result<GetNodeBatchResponse>>> GetNodeBatch([FromBody] GetNodeBatchRequest request, Guid noteGraphId, CancellationToken ct)
        {
            var batchResponse = await _noteNodeService.GetNodeBatchByIds(noteGraphId, request.NodeIds, ct);
            if (!batchResponse.Success || batchResponse.Value == null)
                return BadRequest(Result<GetNodeBatchResponse>.Fail("Failed to fetch node batch."));

            return Result<GetNodeBatchResponse>.Ok(batchResponse.Value);
        }

        [HttpPost("id/{noteGraphId:guid}/node/create")]
        public async Task<ActionResult<Result<CreateNodeResponse>>> AddNode([FromBody] CreateNodeRequest createNodeRequest, Guid noteGraphId, CancellationToken ct)
        {
            var createNodeResponse = await _noteNodeService.CreateNodeByGraphId(createNodeRequest, noteGraphId, ct);
            if (!createNodeResponse.Success || createNodeResponse.Value == null)
            {
                return Result<CreateNodeResponse>.Fail("Failed to create node.");
            }

            return Result<CreateNodeResponse>.Ok(createNodeResponse.Value);
        }

        [HttpDelete("id/{noteGraphId:guid}/node/delete/{noteNodeId:guid}")]
        public async Task<ActionResult<Result<DeleteNodeResponse>>> DeleteNode(Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            var deleteNodeResponse = await _noteNodeService.DeleteNodeByIds(noteGraphId, noteNodeId, ct);
            if (!deleteNodeResponse.Success || deleteNodeResponse.Value == null)
            {
                return Result<DeleteNodeResponse>.Fail("Failed to delete node of given id");
            }

            return Result<DeleteNodeResponse>.Ok(deleteNodeResponse.Value);
        }

        // This will be called everytime after user stops typing for ~20s
        [HttpPatch("id/{noteGraphId:guid}/node/{nodeId:guid}/content")]
        public async Task<ActionResult<Result<SaveNodeContentResponse>>> SaveNodeContent([FromBody] SaveNodeContentRequest saveNodeContentRequest, Guid noteGraphId, Guid nodeId, CancellationToken ct)
        {
            var saveNodeContentResponse = await _noteNodeService.SaveNodeContentAsync(saveNodeContentRequest, noteGraphId, nodeId, ct);
            if (!saveNodeContentResponse.Success || saveNodeContentResponse.Value == null)
            {
                return BadRequest(Result<SaveNodeContentResponse>.Fail("Failed to save note node."));
            }

            return Result<SaveNodeContentResponse>.Ok(saveNodeContentResponse.Value);
        }

        [HttpPatch("id/{noteGraphId:guid}/node/{nodeId:guid}/metadata")]
        public async Task<ActionResult<Result<EditNodeMetadataResponse>>> EditNoteMetadata([FromBody] EditNodeMetadataRequest editNodeMetadataRequest, Guid noteGraphId, Guid nodeId, CancellationToken ct)
        {
            var editNodeMetadataResponse = await _noteNodeService.EditNodeMetadataByIds(editNodeMetadataRequest, noteGraphId, nodeId, ct);
            if (!editNodeMetadataResponse.Success || editNodeMetadataResponse.Value == null)
            {
                return BadRequest(Result<EditNodeMetadataResponse>.Fail("Failed to edit node metadata."));
            }

            return Result<EditNodeMetadataResponse>.Ok(editNodeMetadataResponse.Value);
        }

        [HttpPatch("id/{noteGraphId:guid}/node/{nodeId:guid}/metadata/llmanalysis")]
        public async Task<ActionResult<Result<EditNodeMetadataResponse>>> EditNoteByLMMAnalysis(Guid noteGraphId, Guid nodeId, CancellationToken ct)
        {
            var editMetadataResponse = await _llmAnalysisService.AnalyzeNodeAsync(noteGraphId, nodeId, ct);
            if (!editMetadataResponse.Success || editMetadataResponse.Value == null)
            {
                return BadRequest(Result<EditNodeMetadataResponse>.Fail("Failed to edit node metadata."));
            }

            return Result<EditNodeMetadataResponse>.Ok(editMetadataResponse.Value);
        }

        [HttpGet("id/{noteGraphId:guid}/nodes/pinned")]
        public async Task<ActionResult<Result<GetPinnedNodesResponse>>> GetPinnedNodes(Guid noteGraphId, CancellationToken ct)
        {
            var result = await _noteNodeService.GetPinnedNodes(noteGraphId, ct);
            if (!result.Success || result.Value == null)
                return BadRequest(Result<GetPinnedNodesResponse>.Fail(result.Error ?? "Failed to get pinned nodes."));

            return Result<GetPinnedNodesResponse>.Ok(result.Value);
        }

        [HttpPatch("id/{noteGraphId:guid}/node/{nodeId:guid}/pin")]
        public async Task<ActionResult<Result<SetPinnedResponse>>> SetNodePinned([FromBody] SetPinnedRequest request, Guid noteGraphId, Guid nodeId, CancellationToken ct)
        {
            var result = await _noteNodeService.SetNodePinned(noteGraphId, nodeId, request.IsPinned, ct);
            if (!result.Success || result.Value == null)
                return BadRequest(Result<SetPinnedResponse>.Fail(result.Error ?? "Failed to update pin status."));

            return Result<SetPinnedResponse>.Ok(result.Value);
        }

        [HttpPost("id/{noteGraphId:guid}/node/paste")]
        public async Task<ActionResult<Result<CreateNodeResponse>>> PasteContentToNode([FromBody] CreateNotePastedRequest createNotePastedRequest, Guid noteGraphId, CancellationToken ct)
        {
            var createNodeResponse = await _noteNodeService.CreateNodeFromPastedContent(createNotePastedRequest, noteGraphId, ct);
            if (!createNodeResponse.Success || createNodeResponse.Value == null)
            {
                return Result<CreateNodeResponse>.Fail("Failed to create node.");
            }

            return Result<CreateNodeResponse>.Ok(createNodeResponse.Value);
        }
    }
}
