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
