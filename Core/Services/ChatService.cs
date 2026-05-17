using graphnotelm.Core.Models;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure.Repository.Contracts;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text;

namespace graphnotelm.Core.Services
{
    public class ChatService : IChatService
    {
        private readonly IChatClient _chatClient;
        private readonly INoteGraphRepository _noteGraphRepository;

        public ChatService(IChatClient chatClient, INoteGraphRepository noteGraphRepository)
        {
            _chatClient = chatClient;
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

            var messages = new List<ChatMessage> { new(ChatRole.System, systemPrompt) };
            messages.AddRange(messageHistory.Select(m => new ChatMessage(
                m.Role == "user" ? ChatRole.User : ChatRole.Assistant,
                m.Content)));

            await foreach (var update in _chatClient.GetStreamingResponseAsync(messages, cancellationToken: cancellationToken))
            {
                if (update.Text is not null)
                    yield return update.Text;
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
