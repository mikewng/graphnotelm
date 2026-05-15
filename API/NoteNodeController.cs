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

        [HttpPatch("id/{noteGraphId:guid}/node/edit/{noteNodeId:guid}")]
        public async Task<ActionResult<Result<EditNodeResponse>>> EditNode([FromBody] EditNodeRequest editNodeRequest, Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            var editNodeResponse = await _noteNodeService.EditNodeByIds(editNodeRequest, noteGraphId, noteNodeId, ct);
            if (!editNodeResponse.Success || editNodeResponse.Value == null)
            {
                return Result<EditNodeResponse>.Fail("Failed to edit node.");
            }

            return Result<EditNodeResponse>.Ok(editNodeResponse.Value);
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
    }
}
