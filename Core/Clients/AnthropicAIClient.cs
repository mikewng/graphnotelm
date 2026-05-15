using System.Text.Json.Serialization;

namespace graphnotelm.Core.Clients
{
    public class AnthropicAIClient : ILLMClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _model;

        public AnthropicAIClient(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://api.anthropic.com/");
            _httpClient.DefaultRequestHeaders.Add(
                "x-api-key", config["Anthropic:ApiKey"]);
            _httpClient.DefaultRequestHeaders.Add(
                "anthropic-version", "2023-06-01");

            _model = config["Anthropic:Model"] ?? "claude-sonnet-4-20250514";
        }

        public async Task<string> CompleteAsync(
        string systemPrompt, string userPrompt)
        {
            var request = new AnthropicRequest
            {
                Model = _model,
                MaxTokens = 4096,
                System = systemPrompt,
                Messages = new[]
            {
                new AnthropicMessage
                {
                    Role = "user",
                    Content = userPrompt
                }
            }
            };

            var response = await _httpClient.PostAsJsonAsync(
                "v1/messages", request);

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<AnthropicResponse>();

            if (result?.Content == null || result.Content.Length == 0)
                throw new InvalidOperationException(
                    "LLM returned empty response.");

            return string.Join("",
                result.Content
                    .Where(c => c.Type == "text")
                    .Select(c => c.Text));
        }


        private class AnthropicRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = "";

            [JsonPropertyName("max_tokens")]
            public int MaxTokens { get; set; }

            [JsonPropertyName("system")]
            public string System { get; set; } = "";

            [JsonPropertyName("messages")]
            public AnthropicMessage[] Messages { get; set; } = [];
        }

        private class AnthropicMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = "";

            [JsonPropertyName("content")]
            public string Content { get; set; } = "";
        }

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
    }
}
