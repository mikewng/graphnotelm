using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace graphnotelm.API
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class NoteGraphController : ControllerBase
    {
        private readonly ILogger<NoteGraphController> _logger;
        private readonly INoteGraphService _noteGraphService;

        public NoteGraphController(ILogger<NoteGraphController> logger, INoteGraphService noteGraphService)
        {
            _logger = logger;
            _noteGraphService = noteGraphService;
        }

        // GENERAL CRUD ENDPOINTS

        [HttpGet("id/{noteGraphId:guid}", Name = "GetNoteGraphById")]
        public async Task<ActionResult<Result<GetGraphResponse>>> GetGraphById(Guid noteGraphId, CancellationToken ct)
        {
            // TODO: NodeGraphService.GetByNoteGraphId(id)
            // 1. Makes a call to SQL DB to see if it exists (and not deleted)
            // 2. If exists, return metadata and document from DynamoDB repo

            var graphResponse = await _noteGraphService.GetNoteGraphById(noteGraphId);
            if (!graphResponse.Success || graphResponse.Value == null)
            {
                return Result<GetGraphResponse>.Fail("Could not find note graph of given id.");
            }

            return Result<GetGraphResponse>.Ok(graphResponse.Value);
        }

        [HttpGet("list", Name = "GetNoteGraphList")]
        public async Task<ActionResult<Result<GetGraphListResponse>>> GetGraphList(CancellationToken ct)
        {
            // TODO: NodeGraphService.GetListofNoteGraphs()
            // 1. Queries list of SQL DB for metadata
            // 2. Returns name, id, and tags in response (not deleted)

            var graphListResponse = await _noteGraphService.GetNoteGraphList();
            if (!graphListResponse.Success || graphListResponse.Value == null)
            {
                return Result<GetGraphListResponse>.Fail("Could not find any graphs for given user.");
            }

            return Result<GetGraphListResponse>.Ok(graphListResponse.Value);
        }

        [HttpPost("create", Name = "CreateNoteGraph")]
        public async Task<ActionResult<Result<CreateGraphResponse>>> CreateGraph([FromBody] CreateGraphRequest createGraphRequest, CancellationToken ct)
        {
            // TODO: NodeGraphService.CreateNoteGraph()
            // 1. Creates the metadata for nodegraph
            // 2. Creates the nodegraph document
            var createGraphResponse = await _noteGraphService.CreateNoteGraph(createGraphRequest);
            if (!createGraphResponse.Success || createGraphResponse.Value == null)
            {
                return Result<CreateGraphResponse>.Fail("Failed to create given graph note.");
            }

            return Result<CreateGraphResponse>.Ok(createGraphResponse.Value);
        }

        [HttpPost("delete/{noteGraphId:guid}", Name = "DeleteNoteGraph")]
        public async Task<ActionResult<Result<DeleteGraphResponse>>> DeleteGraph(Guid noteGraphId, CancellationToken ct)
        {
            // TODO: NodeGraphService.DeleteNoteGraph(id)
            // 1. Finds if notegraph id exists within user
            // 2. If exists, set metadata isDeleted as true
            var deleteGraphResponse = await _noteGraphService.DeleteNoteGraphById(noteGraphId);
            if (!deleteGraphResponse.Success || deleteGraphResponse.Value == null)
            {
                return Result<DeleteGraphResponse>.Fail("Could not delete graph of given id.");
            }

            return Result<DeleteGraphResponse>.Ok(deleteGraphResponse.Value);
        }


        // NOTEGRAPH NOTE NODES ENDPOINTS
        [HttpPost("id/{noteGraphId:guid}/node/create")]
        public async Task<ActionResult<Result<CreateNodeGraphResponse>>> AddNode(Guid noteGraphId, CancellationToken ct)
        {
            // TODO: NodeGraphService.CreateNoteNode(id)
            // 1. Finds if notegraph id exists within user
            // 2. If exists, add note request data to as a new node to document
            return Result<CreateNodeGraphResponse>.Ok(new CreateNodeGraphResponse());
        }

        [HttpPatch("id/{noteGraphId:guid}/node/{noteNodeId:guid}")]
        public async Task<ActionResult<Result<CreateNodeGraphResponse>>> EditNode(Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            // TODO: NodeGraphService.EditNoteNode(notegraph_id, noteid)
            // 1. Finds if notegraph id exists within user
            // 2. If exists, set metadata isDeleted as true
            return Result<CreateNodeGraphResponse>.Ok(new CreateNodeGraphResponse());
        }

        [HttpDelete("id/{noteGraphId:guid}/node/{noteNodeId:guid}")]
        public async Task<ActionResult<Result<CreateNodeGraphResponse>>> DeleteNode(Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            // TODO: NodeGraphService.DeleteNoteNode(id)
            // 1. Finds if notegraph id exists within user
            // 2. If exists, set metadata isDeleted as true
            return Result<CreateNodeGraphResponse>.Ok(new CreateNodeGraphResponse());
        }



        // NOTEGRAPH TAGS
        [HttpGet("id/{noteGraphId:guid}/tags", Name = "GetListOfTags")]
        public async Task<ActionResult<Result<CreateNodeGraphResponse>>> GetListTags(Guid noteGraphId, CancellationToken ct)
        {
            // TODO: NodeGraphService.GetTagsList(id)
            // 1. Finds if notegraph id exists within user
            // 2. If exists, set metadata isDeleted as true
            return Result<CreateNodeGraphResponse>.Ok(new CreateNodeGraphResponse());
        }

        [HttpPatch("id/{noteGraphId:guid}/tags/edit")]
        public async Task<ActionResult<Result<CreateNodeGraphResponse>>> EditTag(Guid noteGraphId, string tag, CancellationToken ct)
        {
            // TODO: NodeGraphService.GetRelationshipsList(id)
            // 1. Finds if notegraph id exists within user
            // 2. If exists, set metadata isDeleted as true
            return Result<CreateNodeGraphResponse>.Ok(new CreateNodeGraphResponse());
        }

        [HttpDelete("id/{noteGraphId:guid}/tags")]
        public async Task<ActionResult<Result<CreateNodeGraphResponse>>> DeleteTag(Guid noteGraphId, string tag, CancellationToken ct)
        {
            // TODO: NodeGraphService.GetRelationshipsList(id)
            // 1. Finds if notegraph id exists within user
            // 2. If exists, set metadata isDeleted as true
            return Result<CreateNodeGraphResponse>.Ok(new CreateNodeGraphResponse());
        }



        [HttpGet("id/{noteGraphId:guid}/relationships", Name = "GetListOfRelationships")]
        public async Task<ActionResult<Result<CreateNodeGraphResponse>>> GetListRelationships(Guid noteGraphId, CancellationToken ct)
        {
            // TODO: NodeGraphService.GetRelationshipsList(id)
            // 1. Finds if notegraph id exists within user
            // 2. If exists, set metadata isDeleted as true
            return Result<CreateNodeGraphResponse>.Ok(new CreateNodeGraphResponse());
        }

        [HttpPatch("id/{noteGraphId:guid}/relationships/edit")]
        public async Task<ActionResult<Result<CreateNodeGraphResponse>>> EditRelationship(Guid noteGraphId, string tag, CancellationToken ct)
        {
            // TODO: NodeGraphService.GetRelationshipsList(id)
            // 1. Finds if notegraph id exists within user
            // 2. If exists, set metadata isDeleted as true
            return Result<CreateNodeGraphResponse>.Ok(new CreateNodeGraphResponse());
        }

        [HttpDelete("id/{noteGraphId:guid}/relationships")]
        public async Task<ActionResult<Result<CreateNodeGraphResponse>>> DeleteRelationship(Guid noteGraphId, string tag, CancellationToken ct)
        {
            // TODO: NodeGraphService.GetRelationshipsList(id)
            // 1. Finds if notegraph id exists within user
            // 2. If exists, set metadata isDeleted as true
            return Result<CreateNodeGraphResponse>.Ok(new CreateNodeGraphResponse());
        }
    }
}
