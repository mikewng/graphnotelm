using graphnotelm.Core.Models;
using graphnotelm.Core.Utils;
using System.ComponentModel;

namespace graphnotelm.Core.Utils.Tools
{
    public record NodeSummary(
        Guid Id,
        string Title,
        float ConfidenceScore
    );

    public class GraphAnalysisTools
    {
        private readonly NoteGraphDocument _document;
        private readonly GraphView _view;

        public GraphAnalysisTools(NoteGraphDocument document)
        {
            _document = document;
            _view = new GraphView(document);
        }

        [Description("Finds the path of lowest-confidence nodes from a starting node using Dijkstra's algorithm. Useful for identifying what the user should study next.")]
        public IReadOnlyList<NodeSummary> FindWeakestPath(
            [Description("The ID of the node to start from.")]
            Guid noteNodeId)
        {
            var path = PathingAlgorithms.DijkstrasById(noteNodeId, _view);

            return path
                .Where(p => _document.Nodes.ContainsKey(p.Id))
                .Select(p =>
                {
                    var node = _document.Nodes[p.Id];
                    return new NodeSummary(node.Id, node.Title, node.Metadata.UserConfidenceRate);
                })
                .ToList();
        }

        [Description("Finds nodes reachable from a starting node whose confidence is at or above the given threshold, using BFS. Useful for exploring what the user already understands well.")]
        public IReadOnlyList<NodeSummary> FindKnowledgeFrontier(
            [Description("The ID of the node to start from.")]
            Guid noteNodeId,
            [Description("Minimum confidence score (0–10) a node must have to be included. Defaults to 3.")]
            float minConfidence = 3.0f)
        {
            var nodeIds = PathingAlgorithms.BreadthFirstSearchById(noteNodeId, minConfidence, _view);

            return nodeIds
                .Where(id => _document.Nodes.ContainsKey(id))
                .Select(id =>
                {
                    var node = _document.Nodes[id];
                    return new NodeSummary(node.Id, node.Title, node.Metadata.UserConfidenceRate);
                })
                .ToList();
        }
    }
}
