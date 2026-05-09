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
    public class NoteGraphTagController : ControllerBase
    {
        private readonly ILogger<NoteGraphTagController> _logger;
        private readonly INoteGraphTagService _graphTagService;

        public NoteGraphTagController(
            ILogger<NoteGraphTagController> logger,
            INoteGraphTagService graphTagService)
        {
            _logger = logger;
            _graphTagService = graphTagService;
        }

        [HttpGet("id/{noteGraphId:guid}/tags", Name = "GetListOfTags")]
        public async Task<ActionResult<Result<GetTagListResponse>>> GetListTags(Guid noteGraphId, CancellationToken ct)
        {
            var getTagListResponse = await _graphTagService.GetTagListByGraphId(noteGraphId, ct);
            if (!getTagListResponse.Success || getTagListResponse.Value == null)
            {
                return Result<GetTagListResponse>.Fail("Failed to retreive tags within graph node.");
            }

            return Result<GetTagListResponse>.Ok(new GetTagListResponse());
        }

        [HttpPost("id/{noteGraphId:guid}/tags/create")]
        public async Task<ActionResult<Result<CreateTagResponse>>> CreateTag([FromBody] CreateTagRequest createTagRequest, Guid noteGraphId, CancellationToken ct)
        {
            var createTagResponse = await _graphTagService.CreateTagByGraphId(createTagRequest, noteGraphId, ct);
            if (!createTagResponse.Success || createTagResponse.Value == null)
            {
                return Result<CreateTagResponse>.Fail("Failed to create tag within graph node.");
            }

            return Result<CreateTagResponse>.Ok(createTagResponse.Value);
        }

        [HttpPatch("id/{noteGraphId:guid}/tags/edit/{tagId:guid}")]
        public async Task<ActionResult<Result<EditTagResponse>>> EditTag([FromBody] EditTagRequest editTagRequest, Guid noteGraphId, Guid tagId, CancellationToken ct)
        {
            var editTagResponse = await _graphTagService.EditTagByIds(editTagRequest, noteGraphId, tagId, ct);
            if (!editTagResponse.Success || editTagResponse.Value == null)
            {
                return Result<EditTagResponse>.Fail("Failed to edit tag within graph node.");
            }

            return Result<EditTagResponse>.Ok(editTagResponse.Value);
        }

        [HttpDelete("id/{noteGraphId:guid}/tags/delete/{tagId:guid}")]
        public async Task<ActionResult<Result<DeleteTagResponse>>> DeleteTag(Guid noteGraphId, Guid tagId, CancellationToken ct)
        {
            var deleteTagResponse = await _graphTagService.DeleteTagByIds(noteGraphId, tagId, ct);
            if (!deleteTagResponse.Success || deleteTagResponse.Value == null)
            {
                return Result<DeleteTagResponse>.Fail("Failed to delete tag within graph node.");
            }

            return Result<DeleteTagResponse>.Ok(deleteTagResponse.Value);
        }

        [HttpPost("id/{noteGraphId:guid}/node/{nodeId:guid}/tags")]
        public async Task<ActionResult<Result<AddNodeTagResponse>>> AddTagToNode([FromBody] AddNodeTagRequest addNodeTagRequest, Guid noteGraphId, Guid nodeId, CancellationToken ct)
        {
            var response = await _graphTagService.AddTagToNode(addNodeTagRequest, noteGraphId, nodeId, ct);
            if (!response.Success || response.Value == null)
                return BadRequest(Result<AddNodeTagResponse>.Fail(response.Error ?? "Failed to add tag to node."));

            return Result<AddNodeTagResponse>.Ok(response.Value);
        }

        [HttpDelete("id/{noteGraphId:guid}/node/{nodeId:guid}/tags/{tagId:guid}")]
        public async Task<ActionResult<Result<RemoveNodeTagResponse>>> RemoveTagFromNode(Guid noteGraphId, Guid nodeId, Guid tagId, CancellationToken ct)
        {
            var response = await _graphTagService.RemoveTagFromNode(noteGraphId, nodeId, tagId, ct);
            if (!response.Success || response.Value == null)
                return BadRequest(Result<RemoveNodeTagResponse>.Fail(response.Error ?? "Failed to remove tag from node."));

            return Result<RemoveNodeTagResponse>.Ok(response.Value);
        }
    }
}
