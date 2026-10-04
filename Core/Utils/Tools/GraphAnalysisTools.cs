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
        List<NodeSummary> Cyclic,
        string? Error = null
    );

    public class GraphAnalysisTools
    {
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

        [Description("Orders a target node and every node it depends on so each comes after its prerequisites, using Kahn's topological sort — the order to study in to understand the target. Nodes caught in a prerequisite cycle can't be ordered and are returned in Cyclic. If the request can't be run, Error says why.")]
        public LearningOrderSummary FindLearningOrder(
            [Description("The ID of the node the user wants to understand.")]
            Guid targetNodeId,
            [Description("The relationship type that links nodes to their prerequisites, by its name or its inverse name (e.g. 'prerequisite to' or 'has prerequisite'). Omit to treat every relationship as a prerequisite link.")]
            string? relationshipType = null,
            [Description("Read using the name you gave: false when the relationship goes from the prerequisite to the node that needs it ('A prerequisite to B'), true when it goes from a node to its prerequisite ('B has prerequisite A').")]
            bool pointsToPrerequisite = false)
        {
            if (!_view.HasNode(targetNodeId))
                return new LearningOrderSummary(new(), new(), "No node with that ID exists in this graph.");

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
                    return new LearningOrderSummary(new(), new(), $"No relationship type named '{name}'. Available: {available}.");
                }
            }

            var result = PathingAlgorithms.KahnTopologicalSortById(targetNodeId, _view, Summarize,
                relationshipIds,
                pointsToPrerequisite ? EdgeDirection.Incoming : EdgeDirection.Outgoing);

            return new LearningOrderSummary(result.Order, result.Cyclic);
        }

        private HashSet<Guid> RelationshipsNamed(Func<RelationshipDefinition, string> nameOf, string name)
            => _document.Relationships
                .Where(kv => string.Equals(nameOf(kv.Value)?.Trim(), name, StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Key)
                .ToHashSet();
    }
}
