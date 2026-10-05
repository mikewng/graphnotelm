using graphnotelm.Core.Models;

namespace graphnotelm.Core.Utils
{
    // Which way to follow a relationship edge, relative to how it is stored (source → target).
    public enum EdgeDirection
    {
        Both,
        Outgoing,
        Incoming,
    }

    public class GraphView
    {
        private readonly NoteGraphDocument _document;
        private readonly Dictionary<Guid, List<NodeRelationship>> _adjacency;
        private readonly Dictionary<Guid, List<NodeRelationship>> _reverseAdjacency;
        private readonly Dictionary<Guid, NoteNode> _nodes;
        private readonly DateTime _asOf;

        // Confidence fades with time, so a view reads it as of one moment — now by default.
        public GraphView(NoteGraphDocument graph, DateTime? asOf = null)
        {
            _document = graph;
            _asOf = asOf ?? DateTime.UtcNow;
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
        public bool HasNode(Guid id) => _document.Nodes.ContainsKey(id);
        public IReadOnlyDictionary<Guid, NoteNode> AllNodes => _document.Nodes;
        // Measured from reviews when the node has any, its self-rating otherwise.
        public float GetConfidence(Guid id) => MemoryModel.Confidence(_document.Nodes[id].Metadata, _asOf);

        public List<NodeRelationship> GetOutgoing(Guid nodeId)
            => _adjacency.GetValueOrDefault(nodeId, new());

        public List<NodeRelationship> GetIncoming(Guid nodeId)
            => _reverseAdjacency.GetValueOrDefault(nodeId, new());

        public List<Guid> GetNeighbors(Guid nodeId)
            => GetNeighbors(nodeId, EdgeDirection.Both);

        // Neighbors across edges of the given relationship types (null = every type),
        // following edges forward, backward, or both ways.
        public List<Guid> GetNeighbors(Guid nodeId, EdgeDirection direction, IReadOnlySet<Guid>? relationshipIds = null)
        {
            IEnumerable<NodeRelationship> edges = direction switch
            {
                EdgeDirection.Outgoing => GetOutgoing(nodeId),
                EdgeDirection.Incoming => GetIncoming(nodeId),
                _ => GetOutgoing(nodeId).Concat(GetIncoming(nodeId)),
            };

            return edges
                .Where(e => relationshipIds is null || relationshipIds.Contains(e.RelationshipId))
                .Select(e => e.TargetNodeId)
                .Where(HasNode) // skip edges still pointing at a deleted node
                .Distinct()
                .ToList();
        }

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
