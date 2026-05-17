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

            var request = new
            {
                model = options?.ModelId ?? _model,
                max_tokens = options?.MaxOutputTokens ?? 4096,
                system = systemMsg?.Text ?? "",
                messages = nonSystem.Select(m => new
                {
                    role = m.Role == ChatRole.User ? "user" : "assistant",
                    content = m.Text
                })
            };

            var httpResponse = await _http.PostAsJsonAsync("v1/messages", request, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();

            var result = await httpResponse.Content.ReadFromJsonAsync<AnthropicResponse>(cancellationToken);
            var text = string.Join("", result!.Content
                .Where(c => c.Type == "text")
                .Select(c => c.Text));

            return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
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
                messages = nonSystem.Select(m => new
                {
                    role = m.Role == ChatRole.User ? "user" : "assistant",
                    content = m.Text
                })
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
            public string Text { get; set; } = "";
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
