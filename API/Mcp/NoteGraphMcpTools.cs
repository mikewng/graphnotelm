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

        [McpServerTool(Name = "get_nodes_by_tag")]
        [Description("Get all nodes in a graph that have a specific tag applied. Returns each matching node's ID, title, and full tag list. Use get_graph_skeleton first to see available tag names.")]
        public async Task<string> GetNodesByTagAsync(
            [Description("The GUID of the note graph")] string graphId,
            [Description("Name of the tag to filter by (case-insensitive)")] string tagName,
            CancellationToken ct)
        {
            if (!Guid.TryParse(graphId, out var gId))
                return Error("Invalid graph ID format.");

            var graph = await _graphRepo.GetByIdAsync(gId, ct);
            if (graph is null)
                return Error("Graph not found.");

            var tag = graph.Tags.FirstOrDefault(kv => kv.Value.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
            if (tag.Value is null)
                return Error($"Tag '{tagName}' not found in this graph.");

            var allNodes = await _nodeRepo.GetAllByGraphIdAsync(gId, ct);

            var matches = allNodes
                .Where(n => n.Tags.Contains(tag.Key))
                .Select(n => new { id = n.Id, title = n.Title, tags = n.Tags });

            return JsonSerializer.Serialize(matches);
        }

        [McpServerTool(Name = "update_node")]
        [Description("Update the title and/or note content of a draft node (one whose title starts with '[DRAFT] '). Only draft nodes may be edited via MCP. The '[DRAFT] ' prefix is preserved automatically — do not include it in the new title. Omit title or note to leave them unchanged.")]
        public async Task<string> UpdateNodeAsync(
            [Description("The GUID of the note graph")] string graphId,
            [Description("The GUID of the node to update")] string nodeId,
            [Description("New title for the node, without the '[DRAFT] ' prefix")] string? title = null,
            [Description("New note content for the node")] string? note = null,
            CancellationToken ct = default)
        {
            if (!Guid.TryParse(graphId, out var gId) || !Guid.TryParse(nodeId, out var nId))
                return Error("Invalid ID format.");

            var node = await _nodeRepo.GetByIdAsync(gId, nId, ct);
            if (node is null)
                return Error("Node not found.");

            if (!node.Title.StartsWith("[DRAFT] ", StringComparison.OrdinalIgnoreCase))
                return Error("Only [DRAFT] nodes may be edited via MCP.");

            if (title is not null)
            {
                // Strip prefix if LLM accidentally included it, then re-apply
                var cleanTitle = title.StartsWith("[DRAFT] ", StringComparison.OrdinalIgnoreCase)
                    ? title["[DRAFT] ".Length..]
                    : title;

                if (string.IsNullOrWhiteSpace(cleanTitle))
                    return Error("Title cannot be empty.");

                node.Title = $"[DRAFT] {cleanTitle}";
            }

            if (note is not null)
                node.Note = note;

            try
            {
                await _nodeRepo.SaveAsync(gId, node);
                return JsonSerializer.Serialize(new { id = node.Id, title = node.Title });
            }
            catch (Exception ex)
            {
                return Error($"Failed to update node: {ex.Message}");
            }
        }

        [McpServerTool(Name = "search_nodes")]
        [Description("Search nodes in a graph by keyword. Matches against both title and note content. Returns each matching node's ID, title, a short snippet around the match, and flags indicating whether the title and/or note matched. Use this instead of fetching all nodes when looking for specific content.")]
        public async Task<string> SearchNodesAsync(
            [Description("The GUID of the note graph to search")] string graphId,
            [Description("Keyword or phrase to search for (minimum 2 characters)")] string query,
            CancellationToken ct)
        {
            if (!Guid.TryParse(graphId, out var gId))
                return Error("Invalid graph ID format.");

            if (query.Length < 2)
                return Error("Query must be at least 2 characters.");

            var graph = await _graphRepo.GetByIdAsync(gId, ct);
            if (graph is null)
                return Error("Graph not found.");

            var nodes = await _nodeRepo.SearchAsync(gId, query, ct);

            var results = nodes.Select(n =>
            {
                var matchedTitle = n.Title.Contains(query, StringComparison.OrdinalIgnoreCase);
                var matchedNote  = n.Note.Contains(query, StringComparison.OrdinalIgnoreCase);
                var snippet      = matchedNote ? BuildSnippet(n.Note, query) : n.Title;
                return new
                {
                    id           = n.Id,
                    title        = n.Title,
                    snippet,
                    matchedTitle,
                    matchedNote
                };
            });

            return JsonSerializer.Serialize(results);
        }

        [McpServerTool(Name = "add_nodes")]
        [Description("Add one or more nodes to an existing note graph. Each node's title is automatically prefixed with '[DRAFT] ' so the user can review before publishing. Tags and relationship types are matched to existing graph definitions by name; new ones are created if they don't exist. Relationship targets are resolved by title — use the exact title of an existing node, or the plain title (without '[DRAFT] ') of another node in this same batch. Call get_graph_skeleton first to see existing node titles, tags, and relationship types.")]
        public async Task<string> AddNodesAsync(
            [Description("The GUID of the note graph to add nodes to")] string graphId,
            [Description("Nodes to add. Titles will be prefixed with '[DRAFT] ' automatically.")] List<McpNodeInput> nodes,
            CancellationToken ct)
        {
            if (!Guid.TryParse(graphId, out var gId))
                return Error("Invalid graph ID format.");

            var graph = await _graphRepo.GetByIdAsync(gId, ct);
            if (graph is null)
                return Error("Graph not found.");

            if (nodes.Count == 0)
                return Error("No nodes provided.");

            var existingNodes = await _nodeRepo.GetAllByGraphIdAsync(gId, ct);

            // Build mutable copies of the graph's tag and relationship dictionaries
            var tags          = new Dictionary<Guid, TagDefinition>(graph.Tags);
            var relationships = new Dictionary<Guid, RelationshipDefinition>(graph.Relationships);

            // Name → ID lookups seeded from existing definitions
            var tagNameToId = tags.ToDictionary(kv => kv.Value.Name, kv => kv.Key, StringComparer.OrdinalIgnoreCase);
            var relNameToId = relationships.ToDictionary(kv => kv.Value.Name, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

            // Resolve/create tags (first occurrence of each new name wins)
            foreach (var tagInput in nodes.SelectMany(n => n.Tags))
            {
                if (tagNameToId.ContainsKey(tagInput.Name)) continue;
                var id = Guid.NewGuid();
                tagNameToId[tagInput.Name] = id;
                tags[id] = new TagDefinition { Name = tagInput.Name, Color = tagInput.Color };
            }

            // Resolve/create relationship types (first occurrence wins)
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

            // Title → ID lookup: existing nodes by actual title, new nodes by input title (without [DRAFT])
            var titleToId = existingNodes.ToDictionary(n => n.Title, n => n.Id, StringComparer.OrdinalIgnoreCase);

            // First pass: assign GUIDs and register input titles so cross-batch relationships can resolve
            var noteNodes = new List<NoteNode>();
            foreach (var input in nodes)
            {
                var nodeId = Guid.NewGuid();
                titleToId.TryAdd(input.Title, nodeId);

                noteNodes.Add(new NoteNode
                {
                    Id    = nodeId,
                    Title = $"[DRAFT] {input.Title}",
                    Note  = input.Note,
                    Tags  = input.Tags
                        .Where(t => tagNameToId.ContainsKey(t.Name))
                        .Select(t => tagNameToId[t.Name])
                        .ToList(),
                    Relationships = []
                });
            }

            // Second pass: resolve relationships — all node IDs now known
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

            try
            {
                graph.Tags          = tags;
                graph.Relationships = relationships;
                await _graphRepo.SaveAsync(graph);

                foreach (var node in noteNodes)
                    await _nodeRepo.SaveAsync(gId, node);

                return JsonSerializer.Serialize(noteNodes.Select(n => new { id = n.Id, title = n.Title }));
            }
            catch (Exception ex)
            {
                return Error($"Failed to add nodes: {ex.Message}");
            }
        }

        [McpServerTool(Name = "create_notegraph")]
        [Description("Creates a new note graph with nodes, tags, and relationships. Tags and relationship types are created automatically from the names you provide. Returns the new graph's GUID.")]
        public async Task<string> CreateNoteGraphAsync(
            [Description("Name of the note graph")] string name,
            [Description("Nodes to populate the graph with")] List<McpNodeInput> nodes,
            [Description("Optional short description of the graph")] string? description = null,
            [Description("Optional system prompt that shapes how the AI assistant behaves when chatting with this graph")] string? systemPrompt = null,
            CancellationToken ct = default)
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

        private static string BuildSnippet(string text, string query, int halfWindow = 80)
        {
            var idx = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return text.Length <= halfWindow * 2 ? text : text[..(halfWindow * 2)] + "...";

            var start   = Math.Max(0, idx - halfWindow);
            var end     = Math.Min(text.Length, idx + query.Length + halfWindow);
            var snippet = text[start..end];
            if (start > 0)        snippet = "..." + snippet;
            if (end < text.Length) snippet += "...";
            return snippet;
        }
    }

}
