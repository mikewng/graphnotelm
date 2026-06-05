using graphnotelm.Core.Models;
using graphnotelm.Infrastructure.Repository.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace graphnotelm.API.Mcp
{
    [McpServerToolType]
    public sealed class NoteGraphMcpTools
    {
        private readonly INoteGraphMetadataRepository _metadataRepo;
        private readonly INoteGraphRepository _graphRepo;
        private readonly INoteNodeRepository _nodeRepo;
        private readonly McpSettings _mcpSettings;

        public NoteGraphMcpTools(
            INoteGraphMetadataRepository metadataRepo,
            INoteGraphRepository graphRepo,
            INoteNodeRepository nodeRepo,
            McpSettings mcpSettings)
        {
            _metadataRepo = metadataRepo;
            _graphRepo = graphRepo;
            _nodeRepo = nodeRepo;
            _mcpSettings = mcpSettings;
        }

        [McpServerTool(Name = "list_graphs")]
        [Description("List all available note graphs. Returns each graph's ID, name, and description. Call this first to discover what knowledge graphs exist before querying their content.")]
        public async Task<string> ListGraphsAsync(CancellationToken ct)
        {
            var graphs = _mcpSettings.LocalUserId.HasValue
                ? await _metadataRepo.GetListByUserIdAsync(_mcpSettings.LocalUserId.Value, ct)
                : await _metadataRepo.GetAllAsync(ct);

            return JsonSerializer.Serialize(graphs.Select(g => new
            {
                id          = g.Id,
                name        = g.Name,
                description = g.Description
            }));
        }

        [McpServerTool(Name = "get_graph_skeleton")]
        [Description("Get the structure of a note graph: all node IDs and titles, available tags, and relationship types. Use this to understand the graph's shape and decide which nodes to fetch in full.")]
        public async Task<string> GetGraphSkeletonAsync(
            [Description("The GUID of the note graph, obtained from list_graphs")] string graphId,
            CancellationToken ct)
        {
            if (!Guid.TryParse(graphId, out var id))
                return Error("Invalid graph ID format.");

            var graph = await _graphRepo.GetByIdAsync(id, ct);
            if (graph is null)
                return Error("Graph not found.");

            var nodes = await _nodeRepo.GetAllByGraphIdAsync(id, ct);

            var result = new
            {
                tags = graph.Tags.Select(t => new
                {
                    id   = t.Key,
                    name = t.Value.Name
                }),
                relationshipTypes = graph.Relationships.Select(r => new
                {
                    id      = r.Key,
                    name    = r.Value.Name,
                    inverse = r.Value.Inverse
                }),
                nodes = nodes.Select(n => new
                {
                    id            = n.Id,
                    title         = n.Title,
                    tags          = n.Tags,
                    relationships = n.Relationships.Select(r => new
                    {
                        targetNodeId   = r.TargetNodeId,
                        relationshipId = r.RelationshipId
                    })
                })
            };

            return JsonSerializer.Serialize(result);
        }

        [McpServerTool(Name = "get_node")]
        [Description("Get the full content of a specific node, including its note text, tags, relationships, and confidence metadata.")]
        public async Task<string> GetNodeAsync(
            [Description("The GUID of the note graph")] string graphId,
            [Description("The GUID of the node, obtained from get_graph_skeleton")] string nodeId,
            CancellationToken ct)
        {
            if (!Guid.TryParse(graphId, out var gId) || !Guid.TryParse(nodeId, out var nId))
                return Error("Invalid ID format.");

            var node = await _nodeRepo.GetByIdAsync(gId, nId, ct);
            if (node is null)
                return Error("Node not found.");

            return JsonSerializer.Serialize(new
            {
                id            = node.Id,
                title         = node.Title,
                note          = node.Note,
                tags          = node.Tags,
                relationships = node.Relationships.Select(r => new
                {
                    targetNodeId   = r.TargetNodeId,
                    relationshipId = r.RelationshipId
                }),
                confidenceRate = node.Metadata.UserConfidenceRate
            });
        }

        [McpServerTool(Name = "get_related_nodes")]
        [Description("Get all nodes directly connected to a given node, with the relationship type name and the target node's title. Use this to traverse the knowledge graph from a starting point.")]
        public async Task<string> GetRelatedNodesAsync(
            [Description("The GUID of the note graph")] string graphId,
            [Description("The GUID of the node to traverse from")] string nodeId,
            CancellationToken ct)
        {
            if (!Guid.TryParse(graphId, out var gId) || !Guid.TryParse(nodeId, out var nId))
                return Error("Invalid ID format.");

            var graph = await _graphRepo.GetByIdAsync(gId, ct);
            if (graph is null)
                return Error("Graph not found.");

            var node = await _nodeRepo.GetByIdAsync(gId, nId, ct);
            if (node is null)
                return Error("Node not found.");

            var allNodes = await _nodeRepo.GetAllByGraphIdAsync(gId, ct);
            var nodeIndex = allNodes.ToDictionary(n => n.Id);

            var related = node.Relationships.Select(r =>
            {
                var relName     = graph.Relationships.TryGetValue(r.RelationshipId, out var rel) ? rel.Name : "relates to";
                var targetTitle = nodeIndex.TryGetValue(r.TargetNodeId, out var target) ? target.Title : "unknown";
                return new
                {
                    relationshipType = relName,
                    targetNodeId     = r.TargetNodeId,
                    targetTitle
                };
            });

            return JsonSerializer.Serialize(related);
        }

        private static string Error(string message) =>
            JsonSerializer.Serialize(new { error = message });
    }
}
