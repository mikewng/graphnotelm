using Amazon.DynamoDBv2.Model;
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
        public async Task<ActionResult<Result<CreateNodeResponse>>> AddNode([FromBody] CreateNodeRequest createNodeRequest, Guid noteGraphId, CancellationToken ct)
        {
            // TODO: NodeGraphService.CreateNoteNode(id)
            // 1. Finds if notegraph id exists within user
            // 2. If exists, add note request data to as a new node to document
            var createNodeResponse = await _noteGraphService.CreateNodeByGraphId(createNodeRequest, noteGraphId);
            if (!createNodeResponse.Success || createNodeResponse.Value == null)
            {
                return Result<CreateNodeResponse>.Fail("Failed to create node.");
            }

            return Result<CreateNodeResponse>.Ok(createNodeResponse.Value);
        }

        [HttpPatch("id/{noteGraphId:guid}/node/{noteNodeId:guid}")]
        public async Task<ActionResult<Result<EditNodeResponse>>> EditNode([FromBody] EditNodeRequest editNodeRequest, Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            // TODO: NodeGraphService.EditNoteNode(notegraph_id, noteid)
            // 1. Finds if notegraph id exists within user
            // 2. If exists, set metadata isDeleted as true
            var editNodeResponse = await _noteGraphService.EditNodeByIds(editNodeRequest, noteGraphId, noteNodeId);
            if (!editNodeResponse.Success || editNodeResponse.Value == null)
            {
                return Result<EditNodeResponse>.Fail("Failed to edit node.");
            }

            return Result<EditNodeResponse>.Ok(editNodeResponse.Value);
        }

        [HttpDelete("id/{noteGraphId:guid}/node/{noteNodeId:guid}")]
        public async Task<ActionResult<Result<DeleteNodeResponse>>> DeleteNode(Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            // TODO: NodeGraphService.DeleteNoteNode(id)
            // 1. Finds if notegraph id exists within user
            // 2. If exists, set metadata isDeleted as true
            var deleteNodeResponse = await _noteGraphService.DeleteNodeByIds(noteGraphId, noteNodeId);
            if (!deleteNodeResponse.Success || deleteNodeResponse.Value == null)
            {
                return Result<DeleteNodeResponse>.Fail("Failed to delete node of given id");
            }

            return Result<DeleteNodeResponse>.Ok(deleteNodeResponse.Value);
        }



        // NOTEGRAPH TAGS
        [HttpGet("id/{noteGraphId:guid}/tags", Name = "GetListOfTags")]
        public async Task<ActionResult<Result<GetTagListResponse>>> GetListTags(Guid noteGraphId, CancellationToken ct)
        {
            // TODO: NodeGraphService.GetTagsList(id)
            // 1. Finds if notegraph id exists within user
            // 2. If exists, set metadata isDeleted as true
            var getTagListResponse = await _noteGraphService.GetTagListByGraphId(noteGraphId);
            if (!getTagListResponse.Success || getTagListResponse.Value == null)
            {
                return Result<GetTagListResponse>.Fail("Failed to retreive tags within graph node.");
            }

            return Result<GetTagListResponse>.Ok(new GetTagListResponse());
        }

        [HttpPost("id/{noteGraphId:guid}/tags/create")]
        public async Task<ActionResult<Result<CreateTagResponse>>> CreateTag([FromBody] CreateTagRequest createTagRequest, Guid noteGraphId, CancellationToken ct)
        {
            // TODO: NodeGraphService.GetRelationshipsList(id)
            // 1. Finds if notegraph id exists within user
            // 2. If exists, set metadata isDeleted as true
            var createTagResponse = await _noteGraphService.CreateTagByGraphId(createTagRequest, noteGraphId);
            if (!createTagResponse.Success || createTagResponse.Value == null)
            {
                return Result<CreateTagResponse>.Fail("Failed to create tag within graph node.");
            }

            return Result<CreateTagResponse>.Ok(createTagResponse.Value);
        }

        [HttpPatch("id/{noteGraphId:guid}/tags/edit/{tagId:guid}")]
        public async Task<ActionResult<Result<EditTagResponse>>> EditTag([FromBody] EditTagRequest editTagRequest, Guid noteGraphId, Guid tagId, CancellationToken ct)
        {
            // TODO: NodeGraphService.GetRelationshipsList(id)
            // 1. Finds if notegraph id exists within user
            // 2. If exists, set metadata isDeleted as true
            var editTagResponse = await _noteGraphService.EditTagByIds(editTagRequest, noteGraphId, tagId);
            if (!editTagResponse.Success || editTagResponse.Value == null)
            {
                return Result<EditTagResponse>.Fail("Failed to edit tag within graph node.");
            }

            return Result<EditTagResponse>.Ok(editTagResponse.Value);
        }

        [HttpDelete("id/{noteGraphId:guid}/tags/delete/{tagId:guid}")]
        public async Task<ActionResult<Result<DeleteTagResponse>>> DeleteTag(Guid noteGraphId, Guid tagId, CancellationToken ct)
        {
            // TODO: NodeGraphService.GetRelationshipsList(id)
            // 1. Finds if notegraph id exists within user
            // 2. If exists, set metadata isDeleted as true
            var deleteTagResponse = await _noteGraphService.DeleteTagByIds(noteGraphId, tagId);
            if (!deleteTagResponse.Success || deleteTagResponse.Value == null)
            {
                return Result<DeleteTagResponse>.Fail("Failed to delete tag within graph node.");
            }

            return Result<DeleteTagResponse>.Ok(deleteTagResponse.Value);
        }



        [HttpGet("id/{noteGraphId:guid}/relationships", Name = "GetListOfRelationships")]
        public async Task<ActionResult<Result<GetRelationshipListResponse>>> GetListRelationships(Guid noteGraphId, CancellationToken ct)
        {
            // TODO: NodeGraphService.GetRelationshipsList(id)
            // 1. Finds if notegraph id exists within user
            // 2. If exists, set metadata isDeleted as true
            var getRelationshipListResponse = await _noteGraphService.GetRelationshipListByGraphId(noteGraphId);
            if (!getRelationshipListResponse.Success || getRelationshipListResponse.Value == null)
            {
                return Result<GetRelationshipListResponse>.Fail("Failed to retreive relationships within graph node.");
            }

            return Result<GetRelationshipListResponse>.Ok(getRelationshipListResponse.Value);
        }

        [HttpPatch("id/{noteGraphId:guid}/relationships/edit/{relationId:guid}")]
        public async Task<ActionResult<Result<EditRelationshipResponse>>> EditRelationship(EditRelationshipRequest editRelationshipRequest, Guid noteGraphId, Guid relationId, CancellationToken ct)
        {
            // TODO: NodeGraphService.GetRelationshipsList(id)
            // 1. Finds if notegraph id exists within user
            // 2. If exists, set metadata isDeleted as true
            var editRelationshipResponse = await _noteGraphService.EditRelationshipByIds(editRelationshipRequest, noteGraphId, relationId);
            if (!editRelationshipResponse.Success || editRelationshipResponse.Value == null)
            {
                return Result<EditRelationshipResponse>.Fail("Failed to edit relationships within graph node.");
            }

            return Result<EditRelationshipResponse>.Ok(editRelationshipResponse.Value);
        }

        [HttpDelete("id/{noteGraphId:guid}/relationships")]
        public async Task<ActionResult<Result<DeleteRelationshipResponse>>> DeleteRelationship(Guid noteGraphId, Guid relationId, CancellationToken ct)
        {
            // TODO: NodeGraphService.GetRelationshipsList(id)
            // 1. Finds if notegraph id exists within user
            // 2. If exists, set metadata isDeleted as true
            var deleteRelationshipResponse = await _noteGraphService.DeleteRelationshipByIds(noteGraphId, relationId);
            if (!deleteRelationshipResponse.Success || deleteRelationshipResponse.Value == null)
            {
                return Result<DeleteRelationshipResponse>.Fail("Failed to delete relationships within graph node.");
            }

            return Result<DeleteRelationshipResponse>.Ok(new DeleteRelationshipResponse());
        }
    }
}
