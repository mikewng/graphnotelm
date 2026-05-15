using graphnotelm.Core.Clients;
using graphnotelm.Core.Models;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Utils;
using System.Text.Json;

namespace graphnotelm.Core.Services
{
    public class GraphContextService : IGraphContextService
    {
        private readonly ILLMClient _llm;

        public async Task<Result<GraphContext>> InferContextAsync(NoteGraphDocument graph)
        {
            var sampleNodes = graph.Nodes.Values
                .Take(5)
                .Select(n => new
                {
                    n.Title,
                    Note = n.Note.Length > 200 ? n.Note[..200] : n.Note,
                    Edges = n.Relationships.Select(r =>
                    {
                        var relName = graph.Relationships
                            .GetValueOrDefault(r.RelationshipId)?.Name ?? "related to";
                        var targetTitle = graph.Nodes
                            .GetValueOrDefault(r.TargetNodeId)?.Title ?? "unknown";
                        return $"{relName} → {targetTitle}";
                    })
                });

            var relationshipTypes = graph.Relationships.Values
                .Select(r => $"{r.Name} / {r.Inverse}");

            var prompt = $$"""
            Analyze these nodes and relationships from a knowledge graph
            and infer the graph's domain and purpose.

            Nodes:
            {{JsonSerializer.Serialize(sampleNodes, new JsonSerializerOptions { WriteIndented = true })}}

            Relationship types defined:
            {{string.Join(", ", relationshipTypes)}}

            Respond ONLY with a JSON object:
            {
                "domain": "short domain label (e.g. cooking, technical_learning, worldbuilding, game_board)",
                "systemPrompt": "1-2 sentence description of what this graph tracks and how nodes relate",
                "metadataSchemaHint": "comma-separated list of fields the LLM should populate in its analysis, tailored to this domain"
            }
            """;

            var response = await _llm.CompleteAsync(
                "You infer the domain and purpose of knowledge graphs. Respond only with JSON.",
                prompt);

            return Result<GraphContext>.Ok(JsonSerializer.Deserialize<GraphContext>(response) ?? new());
        }
    }
}
