using graphnotelm.Core.Models;
using graphnotelm.Infrastructure.Contracts;
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
        private readonly IUnitOfWork _unitOfWork;
        private readonly McpSettings _mcpSettings;

        public NoteGraphMcpTools(
            INoteGraphMetadataRepository metadataRepo,
            INoteGraphRepository graphRepo,
            INoteNodeRepository nodeRepo,
            IUnitOfWork unitOfWork,
            McpSettings mcpSettings)
        {
            _metadataRepo = metadataRepo;
            _graphRepo = graphRepo;
            _nodeRepo = nodeRepo;
            _unitOfWork = unitOfWork;
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

        [McpServerTool(Name = "get_nodes_list")]
        [Description("Get the full content of multiple nodes in a single call. More efficient than calling get_node repeatedly. Returns a map of nodeId to node content.")]
        public async Task<string> GetNodesListAsync(
            [Description("The GUID of the note graph")] string graphId,
            [Description("List of node GUIDs to fetch, obtained from get_graph_skeleton")] List<string> nodeIds,
            CancellationToken ct)
        {
            if (!Guid.TryParse(graphId, out var gId))
                return Error("Invalid graph ID format.");

            var guids = nodeIds
                .Where(id => Guid.TryParse(id, out _))
                .Select(Guid.Parse)
                .ToList();

            if (guids.Count == 0)
                return Error("No valid node IDs provided.");

            var tasks = guids.Select(nId => _nodeRepo.GetByIdAsync(gId, nId, ct));
            var results = await Task.WhenAll(tasks);

            var nodes = guids.Zip(results, (id, node) => (id, node))
                .Where(x => x.node is not null)
                .ToDictionary(
                    x => x.id.ToString(),
                    x => new
                    {
                        id            = x.node!.Id,
                        title         = x.node.Title,
                        note          = x.node.Note,
                        tags          = x.node.Tags,
                        relationships = x.node.Relationships.Select(r => new
                        {
                            targetNodeId   = r.TargetNodeId,
                            relationshipId = r.RelationshipId
                        }),
                        confidenceRate = x.node.Metadata.UserConfidenceRate
                    });

            return JsonSerializer.Serialize(nodes);
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

        [McpServerTool(Name = "create_notegraph")]
        [Description("Creates a new note graph with nodes, tags, and relationships. Tags and relationship types are created automatically from the names you provide. Returns the new graph's GUID.")]
        public async Task<string> CreateNoteGraphAsync(
            [Description("Name of the note graph")] string name,
            [Description("Nodes to populate the graph with")] List<McpNodeInput> nodes,
            [Description("Optional short description of the graph")] string? description,
            [Description("Optional system prompt that shapes how the AI assistant behaves when chatting with this graph")] string? systemPrompt,
            CancellationToken ct)
        {
            if (_mcpSettings.LocalUserId is null)
                return Error("MCP user is not configured. Call POST /settings/mcp/configure-user first.");

            if (string.IsNullOrWhiteSpace(name))
                return Error("Name is required.");

            var userId = _mcpSettings.LocalUserId.Value;

            // 1. Unique tag names → GUIDs + LLM-provided color (first occurrence wins)
            var tagNameToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var tags = new Dictionary<Guid, TagDefinition>();
            foreach (var tagInput in nodes.SelectMany(n => n.Tags))
            {
                if (tagNameToId.ContainsKey(tagInput.Name)) continue;
                var id = Guid.NewGuid();
                tagNameToId[tagInput.Name] = id;
                tags[id] = new TagDefinition
                {
                    Name  = tagInput.Name,
                    Color = tagInput.Color
                };
            }

            // 2. Unique relationship names → GUIDs + LLM-provided color + inverse (first occurrence wins)
            var relNameToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var relationships = new Dictionary<Guid, RelationshipDefinition>();
            foreach (var relInput in nodes.SelectMany(n => n.Relationships))
            {
                if (relNameToId.ContainsKey(relInput.RelationshipName)) continue;
                var id = Guid.NewGuid();
                relNameToId[relInput.RelationshipName] = id;
                relationships[id] = new RelationshipDefinition
                {
                    Name    = relInput.RelationshipName,
                    Color   = relInput.Color,
                    Inverse = relInput.InverseRelationshipName ?? string.Empty
                };
            }

            // 3. Assign GUIDs to nodes, build title → ID lookup (first occurrence wins on duplicates)
            var titleToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var noteNodes = new List<NoteNode>();
            foreach (var input in nodes)
            {
                var nodeId = Guid.NewGuid();
                titleToId.TryAdd(input.Title, nodeId);

                noteNodes.Add(new NoteNode
                {
                    Id    = nodeId,
                    Title = input.Title,
                    Note  = input.Note,
                    Tags  = input.Tags
                        .Where(t => tagNameToId.ContainsKey(t.Name))
                        .Select(t => tagNameToId[t.Name])
                        .ToList(),
                    Relationships = []
                });
            }

            // 4. Resolve relationships — second pass, all node IDs now known
            for (int i = 0; i < nodes.Count; i++)
            {
                var input = nodes[i];
                var node  = noteNodes[i];

                node.Relationships = input.Relationships
                    .Where(r =>
                        titleToId.TryGetValue(r.TargetNodeTitle, out var targetId) &&
                        targetId != node.Id &&
                        relNameToId.ContainsKey(r.RelationshipName))
                    .Select(r => new NodeRelationship
                    {
                        TargetNodeId   = titleToId[r.TargetNodeTitle],
                        RelationshipId = relNameToId[r.RelationshipName]
                    })
                    .ToList();
            }

            // 5. Persist
            try
            {
                var metadata = new NoteGraphMetadata
                {
                    Id          = Guid.NewGuid(),
                    UserId      = userId,
                    Name        = name,
                    Description = description ?? string.Empty,
                    IsDeleted   = false
                };

                await _metadataRepo.AddAsync(metadata, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                var document = new NoteGraphDocument
                {
                    Id            = metadata.Id,
                    UserId        = userId,
                    Tags          = tags,
                    Relationships = relationships,
                    Context       = new GraphContext
                    {
                        SystemPrompt = systemPrompt ?? string.Empty
                    }
                };

                await _graphRepo.SaveAsync(document);
                foreach (var node in noteNodes)
                    await _nodeRepo.SaveAsync(metadata.Id, node);

                return metadata.Id.ToString();
            }
            catch (Exception ex)
            {
                return Error($"Failed to create graph: {ex.Message}");
            }
        }

        private static string Error(string message) =>
            JsonSerializer.Serialize(new { error = message });
    }

}
