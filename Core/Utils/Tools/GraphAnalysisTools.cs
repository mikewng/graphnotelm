using graphnotelm.Core.Models;
using graphnotelm.Core.Services.Contracts;
using System.ComponentModel;

namespace graphnotelm.Core.Utils.Tools
{
    public record NodeSummary(
        Guid Id,
        string Title,
        float ConfidenceScore
    );

    public record LearningOrderSummary(
        List<NodeSummary> Order,
        List<List<NodeSummary>> Cycles,
        string? Error = null
    );

    public record ReadyToLearnSummary(
        List<NodeSummary> Ready,
        string? Error = null
    );

    public record BottleneckSummary(
        Guid Id,
        string Title,
        float ConfidenceScore,
        int Dependents
    );

    public record BottlenecksSummary(
        List<BottleneckSummary> Bottlenecks,
        string? Error = null
    );

    public class GraphAnalysisTools
    {
        private const string RelationshipTypeDescription = "The relationship type that links nodes to their prerequisites, by its name or its inverse name (e.g. 'prerequisite to' or 'has prerequisite'). Omit to treat every relationship as a prerequisite link.";
        private const string PointsToPrerequisiteDescription = "Read using the name you gave: false when the relationship goes from the prerequisite to the node that needs it ('A prerequisite to B'), true when it goes from a node to its prerequisite ('B has prerequisite A').";

        private readonly NoteGraphDocument _document;
        private readonly GraphView _view;
        private readonly IGraphAnalysisService _graphAnalysisService;

        public GraphAnalysisTools(NoteGraphDocument document, IGraphAnalysisService graphAnalysisService, GraphView? view = null)
        {
            _document = document;
            _view = view ?? new GraphView(document);
            _graphAnalysisService = graphAnalysisService;
        }

        private NodeSummary Summarize(NoteNode node) => new(node.Id, node.Title, _view.GetConfidence(node.Id));

        [Description("Finds the path from a start node to a target node whose nodes have the lowest total confidence, using Dijkstra's algorithm — the hardest route to the target and the concepts to shore up along the way. Returns an empty list when the two nodes aren't connected.")]
        public IReadOnlyList<NodeSummary> FindWeakestPath(
            [Description("The ID of the node to start from, usually one the user already understands.")]
            Guid startNodeId,
            [Description("The ID of the node the user wants to reach.")]
            Guid targetNodeId)
        {
            return PathingAlgorithms.DijkstrasById(startNodeId, targetNodeId, _view, Summarize);
        }

        [Description("Walks out from a starting node using BFS. Returns the connected nodes the user already understands (confidence at or above the threshold) as Known, and the nodes just past them that fall below it as Frontier — what the user should study next.")]
        public KnowledgeFrontierResult<NodeSummary> FindKnowledgeFrontier(
            [Description("The ID of the node to start from.")]
            Guid noteNodeId,
            [Description("Minimum confidence score (0–10) a node needs to count as understood. Defaults to 3.")]
            float minConfidence = 3.0f)
        {
            return PathingAlgorithms.BreadthFirstSearchById(noteNodeId, minConfidence, _view, Summarize);
        }

        [Description("Orders a target node and every node it depends on so each comes after its prerequisites, using Kahn's topological sort — the order to study in to understand the target. Nodes in a prerequisite cycle depend on one another, so each cycle is kept together in Order and also listed in Cycles; suggest removing an edge to break it. If the request can't be run, Error says why.")]
        public LearningOrderSummary FindLearningOrder(
            [Description("The ID of the node the user wants to understand.")]
            Guid targetNodeId,
            [Description(RelationshipTypeDescription)]
            string? relationshipType = null,
            [Description(PointsToPrerequisiteDescription)]
            bool pointsToPrerequisite = false)
        {
            if (!_view.HasNode(targetNodeId))
                return new LearningOrderSummary(new(), new(), "No node with that ID exists in this graph.");

            var (relationshipIds, direction, error) = ResolvePrerequisites(relationshipType, pointsToPrerequisite);
            if (error is not null)
                return new LearningOrderSummary(new(), new(), error);

            var result = PathingAlgorithms.KahnTopologicalSortById(targetNodeId, _view, Summarize, relationshipIds, direction);
            return new LearningOrderSummary(result.Order, result.Cycles);
        }

        [Description("Finds the notes the user can start learning now, across the whole graph: notes below the confidence threshold whose prerequisites are all at or above it. Notes in a prerequisite cycle count as ready once every prerequisite outside the cycle is known. Unlike FindKnowledgeFrontier, it needs no start node and only follows prerequisite links. If the request can't be run, Error says why.")]
        public ReadyToLearnSummary FindReadyToLearn(
            [Description("Minimum confidence score (0–10) a node needs to count as understood. Defaults to 3.")]
            float minConfidence = 3.0f,
            [Description(RelationshipTypeDescription)]
            string? relationshipType = null,
            [Description(PointsToPrerequisiteDescription)]
            bool pointsToPrerequisite = false)
        {
            var (relationshipIds, direction, error) = ResolvePrerequisites(relationshipType, pointsToPrerequisite);
            if (error is not null)
                return new ReadyToLearnSummary(new(), error);

            return new ReadyToLearnSummary(
                PathingAlgorithms.FindReadyToLearn(minConfidence, _view, Summarize, relationshipIds, direction));
        }

        [Description("Finds the weak notes that hold back the most of the graph — what to study first to unblock the rest. Each is ranked by how many notes depend on it, directly or through others (Dependents), times how far its confidence is below 10. Needs no start or target node. If the request can't be run, Error says why.")]
        public BottlenecksSummary FindBottlenecks(
            [Description("How many notes to return, most holding-back first. Defaults to 10.")]
            int limit = 10,
            [Description(RelationshipTypeDescription)]
            string? relationshipType = null,
            [Description(PointsToPrerequisiteDescription)]
            bool pointsToPrerequisite = false)
        {
            var (relationshipIds, direction, error) = ResolvePrerequisites(relationshipType, pointsToPrerequisite);
            if (error is not null)
                return new BottlenecksSummary(new(), error);

            return new BottlenecksSummary(PathingAlgorithms.FindBottlenecks(_view,
                (node, dependents) => new BottleneckSummary(node.Id, node.Title, _view.GetConfidence(node.Id), dependents),
                Math.Max(1, limit), relationshipIds, direction));
        }

        // Turns the prerequisite relationship the LLM named into the edge types and direction to
        // follow, or an error listing the names it could have used.
        private (HashSet<Guid>? RelationshipIds, EdgeDirection Direction, string? Error) ResolvePrerequisites(
            string? relationshipType, bool pointsToPrerequisite)
        {
            HashSet<Guid>? relationshipIds = null;
            if (!string.IsNullOrWhiteSpace(relationshipType))
            {
                var name = relationshipType.Trim();
                relationshipIds = RelationshipsNamed(rel => rel.Name, name);
                if (relationshipIds.Count == 0)
                {
                    // An inverse name reads each stored edge backwards.
                    relationshipIds = RelationshipsNamed(rel => rel.Inverse, name);
                    pointsToPrerequisite = !pointsToPrerequisite;
                }
                if (relationshipIds.Count == 0)
                {
                    var available = string.Join(", ", _document.Relationships.Values.Select(rel => rel.Name));
                    return (null, default, $"No relationship type named '{name}'. Available: {available}.");
                }
            }

            return (relationshipIds, pointsToPrerequisite ? EdgeDirection.Incoming : EdgeDirection.Outgoing, null);
        }

        private HashSet<Guid> RelationshipsNamed(Func<RelationshipDefinition, string> nameOf, string name)
            => _document.Relationships
                .Where(kv => string.Equals(nameOf(kv.Value)?.Trim(), name, StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Key)
                .ToHashSet();
    }
}
