using Microsoft.Extensions.AI;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace graphnotelm.Core.Clients
{
    public sealed class OpenAICompatibleChatClient : IChatClient
    {
        private readonly HttpClient _http;
        private readonly string _model;
        private readonly string _providerName;

        public OpenAICompatibleChatClient(HttpClient http, string model, string providerName)
        {
            _http = http;
            _model = model;
            _providerName = providerName;
        }

        public ChatClientMetadata Metadata => new(_providerName, _http.BaseAddress, _model);

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var messageList = messages.ToList();
            var tools = options?.Tools?.OfType<AIFunction>().Select(t => new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = t.JsonSchema
                }
            }).ToArray();

            object request = tools?.Length > 0
                ? new
                {
                    model = options?.ModelId ?? _model,
                    messages = messageList.Select(BuildMessage).ToArray(),
                    tools,
                    tool_choice = "auto"
                }
                : new
                {
                    model = options?.ModelId ?? _model,
                    messages = messageList.Select(BuildMessage).ToArray()
                };

            var httpResponse = await _http.PostAsJsonAsync("v1/chat/completions", request, cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException(
                    $"{_providerName} API error ({(int)httpResponse.StatusCode}): {errorBody}",
                    null,
                    httpResponse.StatusCode);
            }

            var result = await httpResponse.Content.ReadFromJsonAsync<OpenAIResponse>(cancellationToken: cancellationToken);
            var choice = result!.Choices.FirstOrDefault();

            var contents = new List<AIContent>();
            if (!string.IsNullOrEmpty(choice?.Message?.Content))
                contents.Add(new TextContent(choice.Message.Content));

            if (choice?.Message?.ToolCalls != null)
            {
                foreach (var tc in choice.Message.ToolCalls)
                {
                    var args = tc.Function?.Arguments != null
                        ? JsonSerializer.Deserialize<Dictionary<string, object>>(tc.Function.Arguments) ?? new()
                        : new Dictionary<string, object>();
                    contents.Add(new FunctionCallContent(tc.Id, tc.Function?.Name ?? "", args));
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
            var request = new
            {
                model = options?.ModelId ?? _model,
                messages = messageList.Select(BuildMessage).ToArray(),
                stream = true
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
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
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

                var data = line[6..];
                if (data == "[DONE]") break;

                OpenAIStreamChunk? chunk;
                try { chunk = JsonSerializer.Deserialize<OpenAIStreamChunk>(data); }
                catch (JsonException) { continue; }

                var delta = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
                if (!string.IsNullOrEmpty(delta))
                    yield return new ChatResponseUpdate(ChatRole.Assistant, delta);
            }
        }

        public object? GetService(Type serviceType, object? key = null)
            => serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose() { }

        private static object BuildMessage(ChatMessage message)
        {
            if (message.Role == ChatRole.Tool)
            {
                var result = message.Contents.OfType<FunctionResultContent>().FirstOrDefault();
                return new
                {
                    role = "tool",
                    tool_call_id = result?.CallId ?? "",
                    content = result?.Result is string s ? s : JsonSerializer.Serialize(result?.Result)
                };
            }

            var role = message.Role == ChatRole.System ? "system"
                     : message.Role == ChatRole.User ? "user"
                     : "assistant";

            if (message.Contents.All(c => c is TextContent))
                return new { role, content = message.Text ?? "" };

            var toolCalls = message.Contents.OfType<FunctionCallContent>().Select(fc => new
            {
                id = fc.CallId,
                type = "function",
                function = new
                {
                    name = fc.Name,
                    arguments = JsonSerializer.Serialize(fc.Arguments ?? new Dictionary<string, object>())
                }
            }).ToArray();

            return toolCalls.Length > 0
                ? (object)new { role, content = message.Text ?? "", tool_calls = toolCalls }
                : new { role, content = message.Text ?? "" };
        }

        // ── Wire models ──────────────────────────────────────────────────────

        private class OpenAIResponse
        {
            [JsonPropertyName("choices")]
            public OpenAIChoice[] Choices { get; set; } = [];
        }

        private class OpenAIChoice
        {
            [JsonPropertyName("message")]
            public OpenAIMessage? Message { get; set; }
        }

        private class OpenAIMessage
        {
            [JsonPropertyName("content")]
            public string? Content { get; set; }

            [JsonPropertyName("tool_calls")]
            public OpenAIToolCall[]? ToolCalls { get; set; }
        }

        private class OpenAIToolCall
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = "";

            [JsonPropertyName("function")]
            public OpenAIFunction? Function { get; set; }
        }

        private class OpenAIFunction
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            [JsonPropertyName("arguments")]
            public string? Arguments { get; set; }
        }

        private class OpenAIStreamChunk
        {
            [JsonPropertyName("choices")]
            public OpenAIStreamChoice[]? Choices { get; set; }
        }

        private class OpenAIStreamChoice
        {
            [JsonPropertyName("delta")]
            public OpenAIStreamDelta? Delta { get; set; }
        }

        private class OpenAIStreamDelta
        {
            [JsonPropertyName("content")]
            public string? Content { get; set; }
        }
    }
}
