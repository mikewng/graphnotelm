using graphnotelm.Core.Clients;
using graphnotelm.Core.Models;
using graphnotelm.Core.Services.Contracts;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;
using System.Net.Http.Headers;

namespace graphnotelm.Core.Services
{
    public class LLMProviderFactory : ILLMProviderFactory
    {
        private readonly ConcurrentDictionary<string, IChatClient> _cache = new();

        public IChatClient GetClient(LLMProviderConfig config)
        {
            var key = $"{config.Provider}|{config.ApiKey}|{config.Model}|{config.Endpoint}";
            return _cache.GetOrAdd(key, _ => CreateClient(config));
        }

        private static IChatClient CreateClient(LLMProviderConfig config) => config.Provider switch
        {
            LLMProviderType.Anthropic => CreateAnthropicClient(config),
            LLMProviderType.OpenAI    => CreateOpenAIClient(config),
            LLMProviderType.Local     => CreateLocalClient(config),
            LLMProviderType.Ollama    => CreateOllamaClient(config),
            _ => throw new InvalidOperationException($"Unknown LLM provider: {config.Provider}")
        };

        private static IChatClient CreateAnthropicClient(LLMProviderConfig config)
        {
            var http = new HttpClient { BaseAddress = new Uri("https://api.anthropic.com/") };
            http.DefaultRequestHeaders.Add("x-api-key", config.ApiKey);
            http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            return new AnthropicChatClient(http, string.IsNullOrWhiteSpace(config.Model)
                ? "claude-sonnet-4-20250514" : config.Model);
        }

        private static IChatClient CreateOpenAIClient(LLMProviderConfig config)
        {
            var http = new HttpClient { BaseAddress = new Uri("https://api.openai.com/") };
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", config.ApiKey);
            return new OpenAICompatibleChatClient(http,
                string.IsNullOrWhiteSpace(config.Model) ? "gpt-4o" : config.Model,
                "openai");
        }

        private static IChatClient CreateLocalClient(LLMProviderConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.Endpoint))
                throw new InvalidOperationException("Local provider requires an Endpoint URL.");

            var http = new HttpClient { BaseAddress = new Uri(config.Endpoint.TrimEnd('/') + "/") };
            if (!string.IsNullOrWhiteSpace(config.ApiKey))
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", config.ApiKey);

            return new OpenAICompatibleChatClient(http,
                string.IsNullOrWhiteSpace(config.Model) ? "local-model" : config.Model,
                "local");
        }

        private static IChatClient CreateOllamaClient(LLMProviderConfig config)
        {
            var endpoint = string.IsNullOrWhiteSpace(config.Endpoint)
                ? "http://localhost:11434/"
                : config.Endpoint.TrimEnd('/') + "/";

            var http = new HttpClient { BaseAddress = new Uri(endpoint) };
            return new OpenAICompatibleChatClient(http,
                string.IsNullOrWhiteSpace(config.Model) ? "llama3.2" : config.Model,
                "ollama");
        }
    }
}
