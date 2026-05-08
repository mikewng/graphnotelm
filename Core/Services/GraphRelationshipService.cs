using graphnotelm.Infrastructure.Contracts;
using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services
{
    public class GraphRelationshipService : IGraphRelationshipService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INoteGraphAccessService _noteGraphAccessService;
        private readonly INoteGraphRepository _noteGraphRepository;

        public GraphRelationshipService(IUnitOfWork unitOfWork, INoteGraphAccessService noteGraphAccessService, INoteGraphRepository noteGraphRepository)
        {
            _unitOfWork = unitOfWork;
            _noteGraphAccessService = noteGraphAccessService;
            _noteGraphRepository = noteGraphRepository;
        }

        public async Task<Result<GetRelationshipListResponse>> GetRelationshipListByGraphId(Guid noteGraphId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return Result<GetRelationshipListResponse>.Fail(metadataResult.Error!);

            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<GetRelationshipListResponse>.Fail(graphDataResult.Error!);

            return Result<GetRelationshipListResponse>.Ok(new GetRelationshipListResponse
            {
                Relationships = graphDataResult.Value!.Relationships.Values.Select(r => r.Name).ToList()
            });
        }

        public async Task<Result<CreateRelationshipResponse>> CreateRelationshipByGraphId(CreateRelationshipRequest createRelationshipRequest, Guid noteGraphId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return Result<CreateRelationshipResponse>.Fail(metadataResult.Error!);

            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<CreateRelationshipResponse>.Fail(graphDataResult.Error!);

            var graphData = graphDataResult.Value!;
            var relId = Guid.NewGuid();
            graphData.Relationships[relId] = new RelationshipDefinition
            {
                Name = createRelationshipRequest.Type,
                Color = createRelationshipRequest.Color,
                Inverse = createRelationshipRequest.Inverse
            };

            try
            {
                await _noteGraphRepository.SaveAsync(graphData);
                return Result<CreateRelationshipResponse>.Ok(new CreateRelationshipResponse());
            }
            catch
            {
                return Result<CreateRelationshipResponse>.Fail("Failed to create relationship.");
            }
        }

        public async Task<Result<EditRelationshipResponse>> EditRelationshipByIds(EditRelationshipRequest editRelationshipRequest, Guid noteGraphId, Guid relationId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return Result<EditRelationshipResponse>.Fail(metadataResult.Error!);

            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<EditRelationshipResponse>.Fail(graphDataResult.Error!);

            var graphData = graphDataResult.Value!;
            if (!graphData.Relationships.TryGetValue(relationId, out var relationship))
                return Result<EditRelationshipResponse>.Fail("Relationship not found.");

            relationship.Name = editRelationshipRequest.Type;
            relationship.Color = editRelationshipRequest.Color;
            relationship.Inverse = editRelationshipRequest.Inverse;

            try
            {
                await _noteGraphRepository.SaveAsync(graphData);
                return Result<EditRelationshipResponse>.Ok(new EditRelationshipResponse());
            }
            catch
            {
                return Result<EditRelationshipResponse>.Fail("Failed to update relationship.");
            }
        }

        public async Task<Result<DeleteRelationshipResponse>> DeleteRelationshipByIds(Guid noteGraphId, Guid relationId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return Result<DeleteRelationshipResponse>.Fail(metadataResult.Error!);

            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<DeleteRelationshipResponse>.Fail(graphDataResult.Error!);

            var graphData = graphDataResult.Value!;
            if (!graphData.Relationships.Remove(relationId))
                return Result<DeleteRelationshipResponse>.Fail("Relationship not found.");

            foreach (var node in graphData.Nodes.Values)
                node.Relationships.RemoveAll(r => r.RelationshipId == relationId);

            try
            {
                await _noteGraphRepository.SaveAsync(graphData);
                return Result<DeleteRelationshipResponse>.Ok(new DeleteRelationshipResponse());
            }
            catch
            {
                return Result<DeleteRelationshipResponse>.Fail("Failed to delete relationship.");
            }
        }

        public async Task<Result<AddNodeRelationshipResponse>> AddRelationshipToNode(AddNodeRelationshipRequest request, Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return Result<AddNodeRelationshipResponse>.Fail(metadataResult.Error!);

            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<AddNodeRelationshipResponse>.Fail(graphDataResult.Error!);

            var graphData = graphDataResult.Value!;
            if (!graphData.Nodes.TryGetValue(noteNodeId, out var node))
                return Result<AddNodeRelationshipResponse>.Fail("Node not found in graph.");
            if (request.TargetNodeId == noteNodeId)
                return Result<AddNodeRelationshipResponse>.Fail("A node cannot have a relationship with itself.");
            if (!graphData.Nodes.ContainsKey(request.TargetNodeId))
                return Result<AddNodeRelationshipResponse>.Fail("Target node not found in graph.");
            if (!graphData.Relationships.ContainsKey(request.RelationshipId))
                return Result<AddNodeRelationshipResponse>.Fail("Relationship type not found in graph.");
            if (node.Relationships.Any(r => r.TargetNodeId == request.TargetNodeId && r.RelationshipId == request.RelationshipId))
                return Result<AddNodeRelationshipResponse>.Fail("This relationship already exists on the node.");

            node.Relationships.Add(new NodeRelationship { TargetNodeId = request.TargetNodeId, RelationshipId = request.RelationshipId });

            try
            {
                await _noteGraphRepository.SaveAsync(graphData);
                return Result<AddNodeRelationshipResponse>.Ok(new AddNodeRelationshipResponse { NodeId = noteNodeId, Relationships = node.Relationships });
            }
            catch
            {
                return Result<AddNodeRelationshipResponse>.Fail("Failed to add relationship to node.");
            }
        }

        public async Task<Result<RemoveNodeRelationshipResponse>> RemoveRelationshipFromNode(Guid noteGraphId, Guid noteNodeId, Guid targetNodeId, Guid relationshipId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return Result<RemoveNodeRelationshipResponse>.Fail(metadataResult.Error!);

            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<RemoveNodeRelationshipResponse>.Fail(graphDataResult.Error!);

            var graphData = graphDataResult.Value!;
            if (!graphData.Nodes.TryGetValue(noteNodeId, out var node))
                return Result<RemoveNodeRelationshipResponse>.Fail("Node not found in graph.");

            var removed = node.Relationships.RemoveAll(r => r.TargetNodeId == targetNodeId && r.RelationshipId == relationshipId);
            if (removed == 0)
                return Result<RemoveNodeRelationshipResponse>.Fail("Relationship not found on node.");

            try
            {
                await _noteGraphRepository.SaveAsync(graphData);
                return Result<RemoveNodeRelationshipResponse>.Ok(new RemoveNodeRelationshipResponse
                {
                    NodeId = noteNodeId,
                    RemovedTargetNodeId = targetNodeId,
                    RemovedRelationshipId = relationshipId
                });
            }
            catch
            {
                return Result<RemoveNodeRelationshipResponse>.Fail("Failed to remove relationship from node.");
            }
        }
    }
}
