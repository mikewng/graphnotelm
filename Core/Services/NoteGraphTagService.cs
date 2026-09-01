using graphnotelm.Infrastructure.Contracts;
using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Models.Mappers;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services
{
    public class NoteGraphTagService : INoteGraphTagService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INoteGraphAccessService _noteGraphAccessService;
        private readonly INoteGraphRepository _noteGraphRepository;
        private readonly INoteNodeRepository _noteNodeRepository;

        public NoteGraphTagService(IUnitOfWork unitOfWork, INoteGraphAccessService noteGraphAccessService, INoteGraphRepository noteGraphRepository, INoteNodeRepository noteNodeRepository)
        {
            _unitOfWork = unitOfWork;
            _noteGraphAccessService = noteGraphAccessService;
            _noteGraphRepository = noteGraphRepository;
            _noteNodeRepository = noteNodeRepository;
        }

        public async Task<Result<GetTagListResponse>> GetTagListByGraphId(Guid noteGraphId, CancellationToken ct)
        {
            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<GetTagListResponse>.Fail(graphDataResult.Error!);

            return Result<GetTagListResponse>.Ok(new GetTagListResponse
            {
                Tags = graphDataResult.Value!.Tags.Values.Select(t => t.Name).ToList()
            });
        }

        public async Task<Result<CreateTagResponse>> CreateTagByGraphId(CreateTagRequest createTagRequest, Guid noteGraphId, CancellationToken ct)
        {
            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<CreateTagResponse>.Fail(graphDataResult.Error!);

            var graphData = graphDataResult.Value!;
            var tagId = Guid.NewGuid();
            var newTag = createTagRequest.ToTagDefinition();
            graphData.Tags[tagId] = newTag;

            try
            {
                await _noteGraphRepository.SaveAsync(graphData);
                return Result<CreateTagResponse>.Ok(newTag.ToCreateTagResponse());
            }
            catch
            {
                return Result<CreateTagResponse>.Fail("Failed to create tag.");
            }
        }

        public async Task<Result<EditTagResponse>> EditTagByIds(EditTagRequest editTagRequest, Guid noteGraphId, Guid tagId, CancellationToken ct)
        {
            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<EditTagResponse>.Fail(graphDataResult.Error!);

            var graphData = graphDataResult.Value!;
            if (!graphData.Tags.TryGetValue(tagId, out var tag))
                return Result<EditTagResponse>.Fail("Tag not found.");

            editTagRequest.ApplyTo(tag);

            try
            {
                await _noteGraphRepository.SaveAsync(graphData);
                return Result<EditTagResponse>.Ok(new EditTagResponse());
            }
            catch
            {
                return Result<EditTagResponse>.Fail("Failed to update tag.");
            }
        }

        public async Task<Result<DeleteTagResponse>> DeleteTagByIds(Guid noteGraphId, Guid tagId, CancellationToken ct)
        {
            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<DeleteTagResponse>.Fail(graphDataResult.Error!);

            var graphData = graphDataResult.Value!;
            if (!graphData.Tags.TryGetValue(tagId, out var tag))
                return Result<DeleteTagResponse>.Fail("Tag not found.");

            graphData.Tags.Remove(tagId);

            // Only nodes carrying the tag need rewriting.
            var affectedNodes = new List<NoteNode>();
            foreach (var node in await _noteNodeRepository.GetNodesReferencingIdAsync(noteGraphId, tagId, ct))
            {
                if (node.Tags.Remove(tagId))
                    affectedNodes.Add(node);
            }

            try
            {
                await _noteGraphRepository.SaveAsync(graphData);
                if (affectedNodes.Count > 0)
                    await _noteNodeRepository.SaveManyAsync(noteGraphId, affectedNodes);
                return Result<DeleteTagResponse>.Ok(tag.ToDeleteTagResponse());
            }
            catch
            {
                return Result<DeleteTagResponse>.Fail("Failed to delete tag.");
            }
        }

        public async Task<Result<AddNodeTagResponse>> AddTagToNode(AddNodeTagRequest request, Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            // Tag definitions live on the document; only the one node is loaded.
            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<AddNodeTagResponse>.Fail(graphDataResult.Error!);

            var graphData = graphDataResult.Value!;
            var node = await _noteNodeRepository.GetByIdAsync(noteGraphId, noteNodeId, ct);
            if (node is null)
                return Result<AddNodeTagResponse>.Fail("Node not found in graph.");
            if (!graphData.Tags.ContainsKey(request.TagId))
                return Result<AddNodeTagResponse>.Fail("Tag not found in graph.");
            if (node.Tags.Contains(request.TagId))
                return Result<AddNodeTagResponse>.Fail("Tag is already assigned to this node.");

            node.Tags.Add(request.TagId);

            try
            {
                await _noteNodeRepository.SaveAsync(noteGraphId, node);
                return Result<AddNodeTagResponse>.Ok(new AddNodeTagResponse { NodeId = noteNodeId, Tags = node.Tags });
            }
            catch
            {
                return Result<AddNodeTagResponse>.Fail("Failed to add tag to node.");
            }
        }

        public async Task<Result<AddTagToNodesResponse>> AddTagToManyNodes(AddTagToNodesRequest request, Guid noteGraphId, CancellationToken ct)
        {
            var graphDataResult = await _noteGraphAccessService.GetAuthorizedFullDocumentAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<AddTagToNodesResponse>.Fail(graphDataResult.Error!);

            var graphData = graphDataResult.Value!;
            var response = new AddTagToNodesResponse();
            var updatedNodes = new List<NoteNode>();

            foreach (var nodeId in request.NodeIds)
            {
                if (!graphData.Nodes.TryGetValue(nodeId, out var node))
                {
                    foreach (var tagId in request.TagIds)
                        response.Skipped.Add(new NodeTagPair { NodeId = nodeId, TagId = tagId });
                    continue;
                }

                var nodeUpdated = false;
                foreach (var tagId in request.TagIds)
                {
                    if (!graphData.Tags.ContainsKey(tagId) || node.Tags.Contains(tagId))
                    {
                        response.Skipped.Add(new NodeTagPair { NodeId = nodeId, TagId = tagId });
                        continue;
                    }

                    node.Tags.Add(tagId);
                    nodeUpdated = true;
                    response.Applied.Add(new NodeTagPair { NodeId = nodeId, TagId = tagId });
                }

                if (nodeUpdated)
                    updatedNodes.Add(node);
            }

            try
            {
                if (updatedNodes.Count > 0)
                    await _noteNodeRepository.SaveManyAsync(noteGraphId, updatedNodes);

                return Result<AddTagToNodesResponse>.Ok(response);
            }
            catch
            {
                return Result<AddTagToNodesResponse>.Fail("Failed to add tags to nodes.");
            }
        }

        public async Task<Result<RemoveTagFromNodesResponse>> RemoveTagFromManyNodes(RemoveTagFromNodesRequest request, Guid noteGraphId, CancellationToken ct)
        {
            var graphDataResult = await _noteGraphAccessService.GetAuthorizedFullDocumentAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<RemoveTagFromNodesResponse>.Fail(graphDataResult.Error!);

            var graphData = graphDataResult.Value!;
            var response = new RemoveTagFromNodesResponse();
            var updatedNodes = new List<NoteNode>();

            foreach (var nodeId in request.NodeIds)
            {
                if (!graphData.Nodes.TryGetValue(nodeId, out var node))
                {
                    foreach (var tagId in request.TagIds)
                        response.Skipped.Add(new NodeTagPair { NodeId = nodeId, TagId = tagId });
                    continue;
                }

                var nodeUpdated = false;
                foreach (var tagId in request.TagIds)
                {
                    if (!node.Tags.Remove(tagId))
                    {
                        response.Skipped.Add(new NodeTagPair { NodeId = nodeId, TagId = tagId });
                        continue;
                    }

                    nodeUpdated = true;
                    response.Applied.Add(new NodeTagPair { NodeId = nodeId, TagId = tagId });
                }

                if (nodeUpdated)
                    updatedNodes.Add(node);
            }

            try
            {
                if (updatedNodes.Count > 0)
                    await _noteNodeRepository.SaveManyAsync(noteGraphId, updatedNodes);

                return Result<RemoveTagFromNodesResponse>.Ok(response);
            }
            catch
            {
                return Result<RemoveTagFromNodesResponse>.Fail("Failed to remove tags from nodes.");
            }
        }

        public async Task<Result<RemoveNodeTagResponse>> RemoveTagFromNode(Guid noteGraphId, Guid noteNodeId, Guid tagId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return Result<RemoveNodeTagResponse>.Fail(metadataResult.Error!);

            var node = await _noteNodeRepository.GetByIdAsync(noteGraphId, noteNodeId, ct);
            if (node is null)
                return Result<RemoveNodeTagResponse>.Fail("Node not found in graph.");
            if (!node.Tags.Remove(tagId))
                return Result<RemoveNodeTagResponse>.Fail("Tag is not assigned to this node.");

            try
            {
                await _noteNodeRepository.SaveAsync(noteGraphId, node);
                return Result<RemoveNodeTagResponse>.Ok(new RemoveNodeTagResponse { NodeId = noteNodeId, RemovedTagId = tagId });
            }
            catch
            {
                return Result<RemoveNodeTagResponse>.Fail("Failed to remove tag from node.");
            }
        }
    }
}
