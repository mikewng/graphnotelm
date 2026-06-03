using graphnotelm.Core.Models;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Core.Utils;
using System.Text;
using System.Text.Json;

namespace graphnotelm.Core.Services
{
    public class LLMContextBuilder : ILLMContextBuilder
    {
        public LLMPrompt BuildNodeAnalysisPrompt(NoteGraphDocument document, GraphView graph, Guid targetNodeId)
        {
            var node = graph.GetNode(targetNodeId);
            var outgoing = graph.GetOutgoing(targetNodeId);
            var incoming = graph.GetIncoming(targetNodeId);
            var existingLLM = node.Metadata.GetRawNamespace("llm");

            var neighborContext = new StringBuilder();
            foreach (var edge in outgoing)
            {
                var target = graph.GetNode(edge.TargetNodeId);
                var relName = document.Relationships.TryGetValue(edge.RelationshipId, out var outRel)
                    ? outRel.Name : "relates to";
                neighborContext.AppendLine(
                    $"- THIS NODE [{relName}] → {target.Title}: \"{target.Note}\"");
            }

            foreach (var edge in incoming)
            {
                // In reverse adjacency, TargetNodeId stores the source node's ID
                var source = graph.GetNode(edge.TargetNodeId);
                var relName = document.Relationships.TryGetValue(edge.RelationshipId, out var inRel)
                    ? inRel.Name : "relates to";
                neighborContext.AppendLine(
                    $"- {source.Title} [{relName}] → THIS NODE: \"{source.Note}\"");
            }

            var systemPrompt = $"""
                You are analyzing a node in a knowledge graph.

                Your job is to analyze this node in context of its
                neighbors and produce structured metadata. You have
                access to previously computed graph metrics and your
                own prior analysis. Build on your prior analysis —
                don't start from scratch each time. Furthermore, always include a 
                'summary' field for a general analysis.

                Respond ONLY with a raw JSON object containing your
                analysis fields. Do NOT wrap it in any key such as
                "llm" — return the object itself directly.
                No markdown, no preamble, no explanation.

                Schema guidance for your output:
                {document.Context.MetadataSchemaHint}
                """;

            var userPrompt = $"""
                ## Target Node

                Title: {node.Title}
                Note: {node.Note}
                User Confidence: {node.Metadata.UserConfidenceRate}

                ## Relationships
                {neighborContext}

                ## Your Prior Analysis (update and improve this)
                {JsonSerializer.Serialize(existingLLM)}
                """;

            return new LLMPrompt
            {
                System = systemPrompt,
                User = userPrompt,
            };
        }

        public LLMPrompt BuildNodeFromPastePrompt(NoteGraphDocument document, string pastedContent)
        {
            var tags = new StringBuilder();
            foreach (var (id, tag) in document.Tags)
                tags.AppendLine($"- {id}: {tag.Name}");

            var relationships = new StringBuilder();
            foreach (var (id, rel) in document.Relationships)
                relationships.AppendLine($"- {id}: {rel.Name} (inverse: {rel.Inverse})");

            var nodes = new StringBuilder();
            foreach (var (id, node) in document.Nodes)
                nodes.AppendLine($"- {id}: {node.Title}");

            var systemPrompt = """
                You are creating a new node for a knowledge graph from pasted content.

                Your job is to:
                1. Extract a concise title and a clean, well-formed note body from the pasted content.
                2. Assign any relevant tags from the available tags list.
                3. Identify any meaningful relationships to existing nodes using the available relationship types.

                Only assign tags and relationships you are confident about. Zero matches for either is acceptable.
                Do NOT invent tag IDs or node IDs — only use the exact GUIDs provided.

                Respond ONLY with a raw JSON object in this exact schema:
                {
                  "title": "string",
                  "note": "string",
                  "tags": ["<guid>", ...],
                  "relationships": [{ "targetNodeId": "<guid>", "relationshipId": "<guid>" }, ...]
                }
                No markdown, no preamble, no explanation.
                """;

            var userPrompt = $"""
                ## Pasted Content
                {pastedContent}

                ## Available Tags
                {(tags.Length > 0 ? tags.ToString() : "(none)")}

                ## Available Relationship Types
                {(relationships.Length > 0 ? relationships.ToString() : "(none)")}

                ## Existing Nodes (title only)
                {(nodes.Length > 0 ? nodes.ToString() : "(none)")}
                """;

            return new LLMPrompt { System = systemPrompt, User = userPrompt };
        }

        public LLMPrompt BuildGraphExtractionPass1Prompt(string content)
        {
            var systemPrompt = """
                You are extracting structured knowledge from a document to build a knowledge graph.

                Extract the following:
                - nodes: the key concepts, entities, or topics in the document. Each node needs a short title, a concise note summarizing that concept, and a list of tag names (from the tags you define) that apply to it.
                - tags: category labels that could apply to multiple nodes (e.g. "Person", "Concept", "Event", "Technology"). Keep the list small and meaningful. Define these BEFORE assigning them to nodes.
                - relationshipTypes: named relationship types that describe how nodes connect (e.g. "influences", "is part of", "causes", "depends on"). Include an inverse label for each.

                Do NOT wire any relationships between nodes yet — that comes in a second step.
                Each node's "tags" array must only contain names that appear in the top-level "tags" list.

                Respond ONLY with a raw JSON object in this exact schema:
                {
                  "graphName": "string",
                  "nodes": [{ "title": "string", "note": "string", "tags": ["tag name", ...] }],
                  "tags": [{ "name": "string" }],
                  "relationshipTypes": [{ "name": "string", "inverse": "string" }]
                }
                No markdown, no preamble, no explanation.
                """;

            return new LLMPrompt { System = systemPrompt, User = content };
        }

        public LLMPrompt BuildGraphExtractionPass2Prompt(string content, Dictionary<Guid, NoteNode> nodes, Dictionary<Guid, RelationshipDefinition> relationshipTypes)
        {
            var nodeList = new StringBuilder();
            foreach (var (id, node) in nodes)
                nodeList.AppendLine($"- {id}: {node.Title}");

            var relTypeList = new StringBuilder();
            foreach (var (id, rel) in relationshipTypes)
                relTypeList.AppendLine($"- {id}: {rel.Name} (inverse: {rel.Inverse})");

            var systemPrompt = """
                You are wiring relationships between nodes in a knowledge graph.

                You will be given:
                - The original document
                - A list of nodes (id: title)
                - A list of relationship types (id: name and inverse)

                Determine which directional relationships exist between nodes based on the document.
                Only use the exact GUIDs provided — do not invent new ones.
                A node may have multiple relationships. Not every node needs to be connected.

                Respond ONLY with a raw JSON object in this exact schema:
                {
                  "relationships": [
                    { "sourceNodeId": "<guid>", "targetNodeId": "<guid>", "relationshipId": "<guid>" }
                  ]
                }
                No markdown, no preamble, no explanation.
                """;

            var userPrompt = $"""
                ## Original Document
                {content}

                ## Nodes
                {nodeList}

                ## Relationship Types
                {relTypeList}
                """;

            return new LLMPrompt { System = systemPrompt, User = userPrompt };
        }

        public LLMPrompt BuildGraphOverviewPrompt(NoteGraphDocument document, GraphView graphView)
        {
            var summaries = new StringBuilder();
            foreach (var (_, node) in graphView.AllNodes)
            {
                var llmData = node.Metadata.GetRawNamespace("llm");
                var summary = llmData?.TryGetProperty("summary", out var s) == true
                    ? s.GetString() ?? ""
                    : Truncate(node.Note, 100);

                summaries.AppendLine(
                    $"[{node.Title}] (confidence: {node.Metadata.UserConfidenceRate})" +
                    $"\n  Summary: {summary}");
            }
            return new LLMPrompt();
        }

        private static string Truncate(string text, int maxLength)
            => text.Length <= maxLength ? text : text[..maxLength] + "…";
    }
}
