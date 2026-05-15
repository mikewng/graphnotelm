using graphnotelm.Core.Clients;
using graphnotelm.Core.Models;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure.Repository.Contracts;
using System.Runtime.CompilerServices;
using System.Text;

namespace graphnotelm.Core.Services
{
    public class ChatService : IChatService
    {
        private readonly ILLMClient _llm;
        private readonly INoteGraphRepository _noteGraphRepository;

        public ChatService(ILLMClient llm, INoteGraphRepository noteGraphRepository)
        {
            _llm = llm;
            _noteGraphRepository = noteGraphRepository;
        }

        public async IAsyncEnumerable<string> StreamResponseAsync(
            Guid userId,
            Guid graphId,
            List<LLMChatMessage> messageHistory,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var document = await _noteGraphRepository.GetByIdAsync(graphId, cancellationToken);

            if (document == null || document.UserId != userId)
            {
                yield return "[Error: graph not found or access denied]";
                yield break;
            }

            var systemPrompt = BuildSystemPrompt(document);

            await foreach (var chunk in _llm.StreamAsync(systemPrompt, messageHistory, cancellationToken))
            {
                yield return chunk;
            }
        }

        private static string BuildSystemPrompt(NoteGraphDocument document)
        {
            var sb = new StringBuilder();

            sb.AppendLine("You are an AI assistant helping the user explore and understand their knowledge graph.");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(document.Context.SystemPrompt))
            {
                sb.AppendLine(document.Context.SystemPrompt);
                sb.AppendLine();
            }

            if (document.Relationships.Count > 0)
            {
                sb.AppendLine("Relationship types:");
                foreach (var (_, rel) in document.Relationships)
                    sb.AppendLine($"  - {rel.Name} (inverse: {rel.Inverse})");
                sb.AppendLine();
            }

            if (document.Tags.Count > 0)
            {
                sb.AppendLine($"Tags: {string.Join(", ", document.Tags.Values.Select(t => t.Name))}");
                sb.AppendLine();
            }

            sb.AppendLine($"Nodes ({document.Nodes.Count} total):");
            foreach (var (_, node) in document.Nodes)
            {
                sb.AppendLine($"  - {node.Title} (confidence: {node.Metadata.UserConfidenceRate:F2})");

                foreach (var rel in node.Relationships)
                {
                    var relName = document.Relationships.TryGetValue(rel.RelationshipId, out var r)
                        ? r.Name : "relates to";
                    var targetTitle = document.Nodes.TryGetValue(rel.TargetNodeId, out var target)
                        ? target.Title : "unknown";
                    sb.AppendLine($"      → [{relName}] {targetTitle}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("Answer questions about this graph's content. Be concise and helpful.");

            return sb.ToString();
        }
    }
}
