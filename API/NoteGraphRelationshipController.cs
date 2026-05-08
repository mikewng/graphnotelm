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
    public class NoteGraphRelationshipController : ControllerBase
    {
        private readonly ILogger<NoteGraphRelationshipController> _logger;
        private readonly IGraphRelationshipService _graphRelationshipService;

        public NoteGraphRelationshipController(
            ILogger<NoteGraphRelationshipController> logger,
            IGraphRelationshipService graphRelationshipService)
        {
            _logger = logger;
            _graphRelationshipService = graphRelationshipService;
        }

        [HttpGet("id/{noteGraphId:guid}/relationships", Name = "GetListOfRelationships")]
        public async Task<ActionResult<Result<GetRelationshipListResponse>>> GetListRelationships(Guid noteGraphId, CancellationToken ct)
        {
            var getRelationshipListResponse = await _graphRelationshipService.GetRelationshipListByGraphId(noteGraphId, ct);
            if (!getRelationshipListResponse.Success || getRelationshipListResponse.Value == null)
            {
                return Result<GetRelationshipListResponse>.Fail("Failed to retreive relationships within graph node.");
            }

            return Result<GetRelationshipListResponse>.Ok(getRelationshipListResponse.Value);
        }

        [HttpPost("id/{noteGraphId:guid}/relationships/create")]
        public async Task<ActionResult<Result<CreateRelationshipResponse>>> CreateRelationship([FromBody] CreateRelationshipRequest createRelationshipRequest, Guid noteGraphId, CancellationToken ct)
        {
            var createRelationshipResponse = await _graphRelationshipService.CreateRelationshipByGraphId(createRelationshipRequest, noteGraphId, ct);
            if (!createRelationshipResponse.Success || createRelationshipResponse.Value == null)
            {
                return Result<CreateRelationshipResponse>.Fail("Failed to create relationships within graph node.");
            }

            return Result<CreateRelationshipResponse>.Ok(createRelationshipResponse.Value);
        }

        [HttpPatch("id/{noteGraphId:guid}/relationships/edit/{relationId:guid}")]
        public async Task<ActionResult<Result<EditRelationshipResponse>>> EditRelationship([FromBody] EditRelationshipRequest editRelationshipRequest, Guid noteGraphId, Guid relationId, CancellationToken ct)
        {
            var editRelationshipResponse = await _graphRelationshipService.EditRelationshipByIds(editRelationshipRequest, noteGraphId, relationId, ct);
            if (!editRelationshipResponse.Success || editRelationshipResponse.Value == null)
            {
                return Result<EditRelationshipResponse>.Fail("Failed to edit relationships within graph node.");
            }

            return Result<EditRelationshipResponse>.Ok(editRelationshipResponse.Value);
        }

        [HttpDelete("id/{noteGraphId:guid}/relationships/delete/{relationId:guid}")]
        public async Task<ActionResult<Result<DeleteRelationshipResponse>>> DeleteRelationship(Guid noteGraphId, Guid relationId, CancellationToken ct)
        {
            var deleteRelationshipResponse = await _graphRelationshipService.DeleteRelationshipByIds(noteGraphId, relationId, ct);
            if (!deleteRelationshipResponse.Success || deleteRelationshipResponse.Value == null)
            {
                return Result<DeleteRelationshipResponse>.Fail("Failed to delete relationships within graph node.");
            }

            return Result<DeleteRelationshipResponse>.Ok(new DeleteRelationshipResponse());
        }
    }
}
