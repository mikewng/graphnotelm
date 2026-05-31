using graphnotelm.Core.Models;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Core.Utils;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text;

namespace graphnotelm.Core.Services
{
    public class ChatService : IChatService
    {
        private readonly IChatClient _chatClient;
        private readonly INoteGraphAccessService _noteGraphAccessService;
        private readonly GraphToolFactory _toolFactory;

        public ChatService(IChatClient chatClient, INoteGraphAccessService noteGraphAccessService, GraphToolFactory toolFactory)
        {
            _chatClient = chatClient;
            _noteGraphAccessService = noteGraphAccessService;
            _toolFactory = toolFactory;
        }

        public async IAsyncEnumerable<AgentEvent> RunAsync(
            Guid graphId,
            IEnumerable<ChatMessage> messageHistory,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var documentResult = await _noteGraphAccessService.GetAuthorizedFullDocumentAsync(graphId, ct);
            if (!documentResult.Success || documentResult.Value == null)
            {
                yield return new ContentDelta("[Error: graph not found or access denied]");
                yield return new TurnComplete();
                yield break;
            }

            var document = documentResult.Value;

            var view = new GraphView(document);
            var tools = _toolFactory.Build(document, view);
            var toolOptions = new ChatOptions { Tools = [.. tools] };

            var messages = new List<ChatMessage> { new(ChatRole.System, BuildSystemPrompt(document)) };
            messages.AddRange(messageHistory);

            // Tool loop — repeated until the model stops calling tools.
            // When it produces a text-only response we capture it and break,
            // avoiding a second API round-trip that re-prompts the model with
            // its own "let me also check" history and causes it to stall.
            string? finalText = null;
            while (true)
            {
                var response = await GetResponseWithRetryAsync(_chatClient, messages, toolOptions, ct);

                var toolCalls = response.Messages
                    .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                    .ToList();

                if (toolCalls.Count == 0)
                {
                    finalText = string.Concat(
                        response.Messages.SelectMany(m => m.Contents.OfType<TextContent>()).Select(t => t.Text));
                    break;
                }

                messages.AddRange(response.Messages);

                var toolTasks = toolCalls
                    .Select(call => (call, tool: tools.FirstOrDefault(t => t.Name == call.Name)))
                    .Where(x => x.tool is not null)
                    .Select(async x =>
                    {
                        var result = await x.tool!.InvokeAsync(
                            new AIFunctionArguments(x.call.Arguments ?? new Dictionary<string, object>()), ct);
                        return (x.call, result);
                    });

                var toolResults = await Task.WhenAll(toolTasks);

                foreach (var (call, result) in toolResults)
                {
                    var args = (call.Arguments as IReadOnlyDictionary<string, object?>)
                               ?? call.Arguments?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                               ?? new Dictionary<string, object?>();
                    yield return new ToolInvoked(call.Name, args);
                    messages.Add(new ChatMessage(ChatRole.Tool,
                        [new FunctionResultContent(call.CallId, result)]));
                    yield return new ToolResult(call.Name);
                }
            }

            // Emit the final answer. The tool loop already has the full response,
            // so yield it directly rather than making an extra streaming API call.
            // Fall back to streaming only if the loop somehow produced no text.
            if (!string.IsNullOrEmpty(finalText))
            {
                yield return new ContentDelta(finalText);
            }
            else
            {
                await foreach (var update in _chatClient.GetStreamingResponseAsync(messages, cancellationToken: ct))
                {
                    if (update.Text is not null)
                        yield return new ContentDelta(update.Text);
                }
            }

            yield return new TurnComplete();
        }

        private static async Task<ChatResponse> GetResponseWithRetryAsync(
            IChatClient client, List<ChatMessage> messages, ChatOptions options, CancellationToken ct)
        {
            for (int attempt = 0; ; attempt++)
            {
                try { return await client.GetResponseAsync(messages, options, ct); }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests && attempt < 3)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt))
                              + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
                    await Task.Delay(delay, ct);
                }
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

            sb.AppendLine($"The graph contains {document.Nodes.Count} nodes.");
            sb.AppendLine("Node titles: " + string.Join(", ", document.Nodes.Values.Select(n => n.Title)));
            sb.AppendLine("Use the available tools to fetch full node content, relationships, and note text. Do not guess or fabricate node content.");

            sb.AppendLine();
            sb.AppendLine("Answer questions about this graph's content. Be concise and helpful.");
            sb.AppendLine("Use tools silently — do not produce text narrating that you are about to call a tool. Only produce text in your final answer after all research is complete.");

            return sb.ToString();
        }
    }
}
