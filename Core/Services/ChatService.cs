using graphnotelm.Core.Models;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Core.Utils;
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
        private readonly GraphToolFactory _toolFactory;

        public ChatService(IChatClient chatClient, INoteGraphRepository noteGraphRepository, GraphToolFactory toolFactory)
        {
            _chatClient = chatClient;
            _noteGraphRepository = noteGraphRepository;
            _toolFactory = toolFactory;
        }

        public async IAsyncEnumerable<AgentEvent> RunAsync(
            Guid userId,
            Guid graphId,
            IEnumerable<ChatMessage> messageHistory,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var document = await _noteGraphRepository.GetByIdAsync(graphId, ct);
            if (document == null || document.UserId != userId)
            {
                yield return new ContentDelta("[Error: graph not found or access denied]");
                yield return new TurnComplete();
                yield break;
            }

            var view = new GraphView(document);
            var tools = _toolFactory.Build(document, view);
            var toolOptions = new ChatOptions { Tools = [.. tools] };

            var messages = new List<ChatMessage> { new(ChatRole.System, BuildSystemPrompt(document)) };
            messages.AddRange(messageHistory);

            // Non-streaming tool loop — runs only when the model decides to call tools.
            // Exits as soon as a response contains no tool calls, leaving messages ready
            // for the streaming final turn below.
            while (true)
            {
                var response = await _chatClient.GetResponseAsync(messages, toolOptions, ct);

                var toolCalls = response.Messages
                    .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                    .ToList();

                if (toolCalls.Count == 0)
                    break;

                messages.AddRange(response.Messages);

                foreach (var call in toolCalls)
                {
                    var tool = tools.FirstOrDefault(t => t.Name == call.Name);
                    if (tool is null) continue;

                    yield return new ToolInvoked(call.Name);

                    var result = await tool.InvokeAsync(
                        new AIFunctionArguments(call.Arguments ?? new Dictionary<string, object>()),
                        ct);

                    messages.Add(new ChatMessage(ChatRole.Tool,
                        [new FunctionResultContent(call.CallId, result)]));

                    yield return new ToolResult(call.Name);
                }
            }

            // Final turn — stream the answer. Tools are omitted so the model responds directly.
            await foreach (var update in _chatClient.GetStreamingResponseAsync(messages, cancellationToken: ct))
            {
                if (update.Text is not null)
                    yield return new ContentDelta(update.Text);
            }

            yield return new TurnComplete();
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
