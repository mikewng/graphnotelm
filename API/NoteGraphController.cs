using graphnotelm.Core.Models.DTOs;
using graphnotelm.Utils;
using Microsoft.AspNetCore.Mvc;

namespace graphnotelm.API
{
    [ApiController]
    [Route("[controller]")]
    public class NoteGraphController : ControllerBase
    {
        private readonly ILogger<NoteGraphController> _logger;

        public NoteGraphController(ILogger<NoteGraphController> logger)
        {
            _logger = logger;
        }

        // GENERAL CRUD ENDPOINTS

        [HttpGet("id/{noteGraphId:guid}", Name = "GetNoteGraphById")]
        public async Task<ActionResult<Result<CreateNodeGraphResponse>>> GetGraphById(Guid noteGraphId, CancellationToken ct)
        {
            // TODO: NodeGraphService.GetByNoteGraphId(id)
            // 1. Makes a call to SQL DB to see if it exists (and not deleted)
            // 2. If exists, return metadata and document from DynamoDB repo
            return Result<CreateNodeGraphResponse>.Ok(new CreateNodeGraphResponse());
        }

        [HttpGet("list", Name = "GetNoteGraphList")]
        public async Task<ActionResult<Result<CreateNodeGraphResponse>>> GetGraphList(Guid noteGraphId, CancellationToken ct)
        {
            // TODO: NodeGraphService.GetListofNoteGraphs()
            // 1. Queries list of SQL DB for metadata
            // 2. Returns name, id, and tags in response (not deleted)
            return Result<CreateNodeGraphResponse>.Ok(new CreateNodeGraphResponse());
        }

        [HttpPost("create", Name = "CreateNoteGraph")]
        public async Task<ActionResult<Result<CreateNodeGraphResponse>>> CreateGraph(Guid noteGraphId, CancellationToken ct)
        {
            // TODO: NodeGraphService.CreateNoteGraph()
            // 1. Creates the metadata for nodegraph
            // 2. Creates the nodegraph document
            return Result<CreateNodeGraphResponse>.Ok(new CreateNodeGraphResponse());
        }

        [HttpPost("delete/{noteGraphId:guid}", Name = "DeleteNoteGraph")]
        public async Task<ActionResult<Result<CreateNodeGraphResponse>>> DeleteGraph(Guid noteGraphId, CancellationToken ct)
        {
            // TODO: NodeGraphService.DeleteNoteGraph(id)
            // 1. Finds if notegraph id exists within user
            // 2. If exists, set metadata isDeleted as true
            return Result<CreateNodeGraphResponse>.Ok(new CreateNodeGraphResponse());
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
