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
    public class NoteGraphFolderController : ControllerBase
    {
        private readonly ILogger<NoteGraphFolderController> _logger;
        private readonly INoteGraphFolderService _graphFolderService;

        public NoteGraphFolderController(
            ILogger<NoteGraphFolderController> logger,
            INoteGraphFolderService graphFolderService)
        {
            _logger = logger;
            _graphFolderService = graphFolderService;
        }

        [HttpGet("id/{noteGraphId:guid}/folders", Name = "GetListOfFolders")]
        public async Task<ActionResult<Result<GetFolderListResponse>>> GetListFolders(Guid noteGraphId, CancellationToken ct)
        {
            var response = await _graphFolderService.GetFolderListByGraphId(noteGraphId, ct);
            if (!response.Success || response.Value == null)
                return BadRequest(Result<GetFolderListResponse>.Fail(response.Error ?? "Failed to retrieve folders within graph."));

            return Result<GetFolderListResponse>.Ok(response.Value);
        }

        [HttpPost("id/{noteGraphId:guid}/folders/create")]
        public async Task<ActionResult<Result<CreateFolderResponse>>> CreateFolder([FromBody] CreateFolderRequest createFolderRequest, Guid noteGraphId, CancellationToken ct)
        {
            var response = await _graphFolderService.CreateFolderByGraphId(createFolderRequest, noteGraphId, ct);
            if (!response.Success || response.Value == null)
                return BadRequest(Result<CreateFolderResponse>.Fail(response.Error ?? "Failed to create folder within graph."));

            return Result<CreateFolderResponse>.Ok(response.Value);
        }

        [HttpPatch("id/{noteGraphId:guid}/folders/edit/{folderId:guid}")]
        public async Task<ActionResult<Result<EditFolderResponse>>> EditFolder([FromBody] EditFolderRequest editFolderRequest, Guid noteGraphId, Guid folderId, CancellationToken ct)
        {
            var response = await _graphFolderService.EditFolderByIds(editFolderRequest, noteGraphId, folderId, ct);
            if (!response.Success || response.Value == null)
                return BadRequest(Result<EditFolderResponse>.Fail(response.Error ?? "Failed to edit folder within graph."));

            return Result<EditFolderResponse>.Ok(response.Value);
        }

        [HttpDelete("id/{noteGraphId:guid}/folders/delete/{folderId:guid}")]
        public async Task<ActionResult<Result<DeleteFolderResponse>>> DeleteFolder(Guid noteGraphId, Guid folderId, CancellationToken ct)
        {
            var response = await _graphFolderService.DeleteFolderByIds(noteGraphId, folderId, ct);
            if (!response.Success || response.Value == null)
                return BadRequest(Result<DeleteFolderResponse>.Fail(response.Error ?? "Failed to delete folder within graph."));

            return Result<DeleteFolderResponse>.Ok(response.Value);
        }

        [HttpPut("id/{noteGraphId:guid}/node/{nodeId:guid}/folder")]
        public async Task<ActionResult<Result<MoveNodeToFolderResponse>>> MoveNodeToFolder([FromBody] MoveNodeToFolderRequest request, Guid noteGraphId, Guid nodeId, CancellationToken ct)
        {
            var response = await _graphFolderService.MoveNodeToFolder(request, noteGraphId, nodeId, ct);
            if (!response.Success || response.Value == null)
                return BadRequest(Result<MoveNodeToFolderResponse>.Fail(response.Error ?? "Failed to move node to folder."));

            return Result<MoveNodeToFolderResponse>.Ok(response.Value);
        }

        [HttpPut("id/{noteGraphId:guid}/nodes/folder")]
        public async Task<ActionResult<Result<MoveNodesToFolderResponse>>> MoveNodesToFolder([FromBody] MoveNodesToFolderRequest request, Guid noteGraphId, CancellationToken ct)
        {
            var response = await _graphFolderService.MoveNodesToFolder(request, noteGraphId, ct);
            if (!response.Success || response.Value == null)
                return BadRequest(Result<MoveNodesToFolderResponse>.Fail(response.Error ?? "Failed to move nodes to folder."));

            return Result<MoveNodesToFolderResponse>.Ok(response.Value);
        }
    }
}
