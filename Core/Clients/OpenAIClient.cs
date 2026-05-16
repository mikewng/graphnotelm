using graphnotelm.Core.Models;

namespace graphnotelm.Core.Clients
{
    public class OpenAIClient : ILLMClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _model;

        public OpenAIClient(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://api.openai.com/");
            _httpClient.DefaultRequestHeaders.Add("x-api-key", config["OpenAI:ApiKey"]);
            _model = config["OpenAI:Model"] ?? "o3";
        }

        public Task<string> CompleteAsync(string systemPrompt, string userPrompt)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<string> StreamAsync(string systemPrompt, IEnumerable<LLMChatMessage> messages, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}
