using graphnotelm.Core.Models;

namespace graphnotelm.Core.Utils
{
    public class GraphView
    {
        private readonly NoteGraphDocument _document;
        private readonly Dictionary<Guid, List<NodeRelationship>> _adjacency;
        private readonly Dictionary<Guid, List<NodeRelationship>> _reverseAdjacency;
        private readonly Dictionary<Guid, NoteNode> _nodes;

        public GraphView(NoteGraphDocument graph)
        {
            _document = graph;
            _nodes = graph.Nodes;
            _adjacency = new();
            _reverseAdjacency = new();

            foreach (var (nodeId, node) in graph.Nodes)
            {
                _adjacency[nodeId] = node.Relationships
                    .Select(r => new NodeRelationship() { TargetNodeId=r.TargetNodeId, RelationshipId=r.RelationshipId})
                    .ToList();

                foreach (var rel in node.Relationships)
                {
                    if (!_reverseAdjacency.ContainsKey(rel.TargetNodeId))
                        _reverseAdjacency[rel.TargetNodeId] = new();

                    _reverseAdjacency[rel.TargetNodeId].Add(new NodeRelationship() { TargetNodeId = nodeId, RelationshipId = rel.RelationshipId });
                }
            }
        }

        // Core accessors the algorithms use
        public NoteNode GetNode(Guid id) => _document.Nodes[id];
        public IReadOnlyDictionary<Guid, NoteNode> AllNodes => _document.Nodes;
        public double GetConfidence(Guid id) => _document.Nodes[id].Metadata.UserConfidenceRate;

        public List<NodeRelationship> GetOutgoing(Guid nodeId)
            => _adjacency.GetValueOrDefault(nodeId, new());

        public List<NodeRelationship> GetIncoming(Guid nodeId)
            => _reverseAdjacency.GetValueOrDefault(nodeId, new());

        public List<Guid> GetNeighbors(Guid nodeId)
            => GetOutgoing(nodeId).Select(e => e.TargetNodeId)
                .Concat(GetIncoming(nodeId).Select(e => e.TargetNodeId))
                .Distinct()
                .ToList();

        // Finds nodes with no incoming edges — root concepts
        public List<Guid> GetRootNodes()
            => _document.Nodes.Keys
                .Where(id => !_reverseAdjacency.ContainsKey(id)
                             || _reverseAdjacency[id].Count == 0)
                .ToList();

        // Finds nodes with no outgoing edges — leaf concepts
        public List<Guid> GetLeafNodes()
            => _document.Nodes.Keys
                .Where(id => !_adjacency.ContainsKey(id)
                             || _adjacency[id].Count == 0)
                .ToList();
    }
}
