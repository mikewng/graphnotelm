using Microsoft.Extensions.AI;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace graphnotelm.Core.Clients
{
    public sealed class AnthropicChatClient : IChatClient
    {
        private readonly HttpClient _http;
        private readonly string _model;

        public AnthropicChatClient(HttpClient http, string model)
        {
            _http = http;
            _model = model;
        }

        public ChatClientMetadata Metadata => new("anthropic", new Uri("https://api.anthropic.com/"), _model);

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var messageList = messages.ToList();
            var systemMsg = messageList.FirstOrDefault(m => m.Role == ChatRole.System);
            var nonSystem = messageList.Where(m => m.Role != ChatRole.System).ToList();

            var tools = options?.Tools?.OfType<AIFunction>().Select(t => new
            {
                name = t.Name,
                description = t.Description,
                input_schema = t.JsonSchema
            }).ToArray();

            var request = new
            {
                model = options?.ModelId ?? _model,
                max_tokens = options?.MaxOutputTokens ?? 4096,
                system = systemMsg?.Text ?? "",
                tools = tools?.Length > 0 ? tools : null,
                messages = nonSystem.Select(BuildAnthropicMessage).ToArray()
            };

            var httpResponse = await _http.PostAsJsonAsync("v1/messages", request, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();

            var result = await httpResponse.Content.ReadFromJsonAsync<AnthropicResponse>(cancellationToken);

            var contents = new List<AIContent>();
            foreach (var block in result!.Content)
            {
                if (block.Type == "text" && block.Text is not null)
                    contents.Add(new TextContent(block.Text));
                else if (block.Type == "tool_use" && block.Id is not null && block.Name is not null)
                {
                    var args = block.Input?.Deserialize<Dictionary<string, object>>()
                               ?? new Dictionary<string, object>();
                    contents.Add(new FunctionCallContent(block.Id, block.Name, args));
                }
            }

            return new ChatResponse(new ChatMessage(ChatRole.Assistant, contents));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var messageList = messages.ToList();
            var systemMsg = messageList.FirstOrDefault(m => m.Role == ChatRole.System);
            var nonSystem = messageList.Where(m => m.Role != ChatRole.System).ToList();

            var request = new
            {
                model = options?.ModelId ?? _model,
                max_tokens = options?.MaxOutputTokens ?? 4096,
                system = systemMsg?.Text ?? "",
                stream = true,
                messages = nonSystem.Select(BuildAnthropicMessage).ToArray()
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
            {
                Content = JsonContent.Create(request)
            };

            using var httpResponse = await _http.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            httpResponse.EnsureSuccessStatusCode();

            using var stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                    continue;

                var data = line[6..];

                AnthropicStreamEvent? evt;
                try { evt = JsonSerializer.Deserialize<AnthropicStreamEvent>(data); }
                catch (JsonException) { continue; }

                if (evt?.Type == "message_stop")
                    break;

                if (evt?.Type == "content_block_delta"
                    && evt.Delta?.Type == "text_delta"
                    && !string.IsNullOrEmpty(evt.Delta.Text))
                {
                    yield return new ChatResponseUpdate(ChatRole.Assistant, evt.Delta.Text);
                }
            }
        }

        public object? GetService(Type serviceType, object? key = null)
            => serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose() { }

        // ── Message serialization ────────────────────────────────────────────

        private static object BuildAnthropicMessage(ChatMessage message)
        {
            // Tool results: ChatRole.Tool → Anthropic "user" role with tool_result content blocks
            if (message.Role == ChatRole.Tool)
            {
                var resultBlocks = message.Contents.OfType<FunctionResultContent>().Select(r =>
                {
                    var content = r.Result is string s ? s : JsonSerializer.Serialize(r.Result);
                    return (object)new { type = "tool_result", tool_use_id = r.CallId, content };
                }).ToArray();

                return new { role = "user", content = resultBlocks };
            }

            var role = message.Role == ChatRole.User ? "user" : "assistant";

            // Simple text-only message — use string form
            if (message.Contents.All(c => c is TextContent))
                return new { role, content = message.Text ?? "" };

            // Mixed content (text + tool calls) — use content blocks array
            var blocks = message.Contents.Select<AIContent, object>(c => c switch
            {
                TextContent t => new { type = "text", text = t.Text ?? "" },
                FunctionCallContent fc => (object)new
                {
                    type = "tool_use",
                    id = fc.CallId,
                    name = fc.Name,
                    input = fc.Arguments ?? new Dictionary<string, object>()
                },
                _ => new { type = "text", text = "" }
            }).ToArray();

            return new { role, content = blocks };
        }

        // ── Anthropic wire models ────────────────────────────────────────────

        private class AnthropicResponse
        {
            [JsonPropertyName("content")]
            public AnthropicContentBlock[] Content { get; set; } = [];
        }

        private class AnthropicContentBlock
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = "";

            [JsonPropertyName("text")]
            public string? Text { get; set; }

            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("input")]
            public JsonElement? Input { get; set; }
        }

        private class AnthropicStreamEvent
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = "";

            [JsonPropertyName("delta")]
            public AnthropicTextDelta? Delta { get; set; }
        }

        private class AnthropicTextDelta
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = "";

            [JsonPropertyName("text")]
            public string? Text { get; set; }
        }
    }
}
