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
                don't start from scratch each time.

                Respond ONLY with a raw JSON object containing your
                analysis fields. Do NOT wrap it in any key such as
                "llm" — return the object itself directly.
                No markdown, no preamble, no explanation.
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
