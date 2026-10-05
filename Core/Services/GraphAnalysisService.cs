using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Models.Mappers;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Core.Utils;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services
{
    public class GraphAnalysisService : IGraphAnalysisService
    {
        private readonly INoteGraphAccessService _noteGraphAccessService;
        private readonly TimeProvider _time;

        public GraphAnalysisService(INoteGraphAccessService noteGraphAccessService, TimeProvider time)
        {
            _noteGraphAccessService = noteGraphAccessService;
            _time = time;
        }

        public GraphView BuildView(NoteGraphDocument document, Guid nodeId)
        {
            return new GraphView(document, _time.GetUtcNow().UtcDateTime);
        }

        // Results report confidence as of the same moment the algorithm used.
        private static Func<NoteNode, AnalysisNodeResult> ToResult(GraphView view)
            => node => node.ToAnalysisNodeResult(view.GetConfidence(node.Id));

        public async Task<Result<WeakestPathResponse>> FindWeakestPath(Guid noteGraphId, WeakestPathRequest request, CancellationToken ct)
        {
            // Analysis only reads titles, confidence and relationships, so skip loading note bodies.
            var documentResult = await _noteGraphAccessService.GetAuthorizedSkeletonDocumentAsync(noteGraphId, ct);
            if (!documentResult.Success)
                return Result<WeakestPathResponse>.Fail(documentResult.Error!);

            var document = documentResult.Value!;
            var error = Validate(document, request.Direction, request.RelationshipIds,
                (request.StartNodeId, "Start"), (request.TargetNodeId, "Target"));
            if (error is not null)
                return Result<WeakestPathResponse>.Fail(error);

            var view = BuildView(document, request.StartNodeId);
            var path = PathingAlgorithms.DijkstrasById(request.StartNodeId, request.TargetNodeId, view,
                ToResult(view), request.Direction, ToFilter(request.RelationshipIds));

            return Result<WeakestPathResponse>.Ok(new WeakestPathResponse { Path = path });
        }

        public async Task<Result<KnowledgeFrontierResponse>> FindKnowledgeFrontier(Guid noteGraphId, KnowledgeFrontierRequest request, CancellationToken ct)
        {
            var documentResult = await _noteGraphAccessService.GetAuthorizedSkeletonDocumentAsync(noteGraphId, ct);
            if (!documentResult.Success)
                return Result<KnowledgeFrontierResponse>.Fail(documentResult.Error!);

            var document = documentResult.Value!;
            var error = Validate(document, request.Direction, request.RelationshipIds, (request.StartNodeId, "Start"));
            if (error is not null)
                return Result<KnowledgeFrontierResponse>.Fail(error);

            var view = BuildView(document, request.StartNodeId);
            var frontier = PathingAlgorithms.BreadthFirstSearchById(request.StartNodeId, request.MinConfidence, view,
                ToResult(view), request.Direction, ToFilter(request.RelationshipIds));

            return Result<KnowledgeFrontierResponse>.Ok(new KnowledgeFrontierResponse
            {
                Known = frontier.Known,
                Frontier = frontier.Frontier
            });
        }

        public async Task<Result<LearningOrderResponse>> FindLearningOrder(Guid noteGraphId, LearningOrderRequest request, CancellationToken ct)
        {
            var documentResult = await _noteGraphAccessService.GetAuthorizedSkeletonDocumentAsync(noteGraphId, ct);
            if (!documentResult.Success)
                return Result<LearningOrderResponse>.Fail(documentResult.Error!);

            var document = documentResult.Value!;
            var error = Validate(document, request.Direction, request.RelationshipIds, (request.TargetNodeId, "Target"));
            if (error is null && request.Direction == EdgeDirection.Both)
                error = "A learning order needs a direction: Outgoing when edges point from a prerequisite to the node that needs it, Incoming when they point the other way.";
            if (error is not null)
                return Result<LearningOrderResponse>.Fail(error);

            var view = BuildView(document, request.TargetNodeId);
            var order = PathingAlgorithms.KahnTopologicalSortById(request.TargetNodeId, view,
                ToResult(view), ToFilter(request.RelationshipIds), request.Direction);

            return Result<LearningOrderResponse>.Ok(new LearningOrderResponse
            {
                Order = order.Order,
                Cyclic = order.Cyclic
            });
        }

        // Returns why the request can't run against this graph, or null when it can.
        private static string? Validate(
            NoteGraphDocument document,
            EdgeDirection direction,
            List<Guid> relationshipIds,
            params (Guid NodeId, string Role)[] nodes)
        {
            foreach (var (nodeId, role) in nodes)
            {
                if (!document.Nodes.ContainsKey(nodeId))
                    return $"{role} node not found in this graph.";
            }

            if (!Enum.IsDefined(direction))
                return $"Unknown edge direction '{direction}'.";

            if (relationshipIds.Any(id => !document.Relationships.ContainsKey(id)))
                return "Relationship type not found in this graph.";

            return null;
        }

        // An empty list means every relationship type.
        private static HashSet<Guid>? ToFilter(List<Guid> relationshipIds)
            => relationshipIds.Count > 0 ? relationshipIds.ToHashSet() : null;
    }
}
