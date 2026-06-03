using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace graphnotelm.API
{
    [Authorize]
    [ApiController]
    [Route("settings/llm")]
    public class LLMSettingsController : ControllerBase
    {
        private const string OllamaDefaultEndpoint = "http://localhost:11434";

        private readonly LLMProviderSettings _providerSettings;
        private readonly IHttpClientFactory _httpClientFactory;

        public LLMSettingsController(LLMProviderSettings providerSettings, IHttpClientFactory httpClientFactory)
        {
            _providerSettings = providerSettings;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("providers")]
        public ActionResult<List<LLMProviderDescriptor>> GetProviders()
        {
            return Ok(new List<LLMProviderDescriptor>
            {
                new()
                {
                    Type = LLMProviderType.Anthropic,
                    RequiresApiKey = true,
                    RequiresEndpoint = false,
                    DefaultModel = "claude-sonnet-4-6",
                    Models = ["claude-opus-4-8", "claude-sonnet-4-6", "claude-haiku-4-5-20251001"]
                },
                new()
                {
                    Type = LLMProviderType.OpenAI,
                    RequiresApiKey = true,
                    RequiresEndpoint = false,
                    DefaultModel = "gpt-4o",
                    Models = ["gpt-4o", "gpt-4o-mini", "gpt-4-turbo"]
                },
                new()
                {
                    Type = LLMProviderType.Local,
                    RequiresApiKey = false,
                    RequiresEndpoint = true,
                    DefaultModel = string.Empty,
                    Models = []
                },
                new()
                {
                    Type = LLMProviderType.Ollama,
                    RequiresApiKey = false,
                    RequiresEndpoint = false,
                    DefaultModel = "llama3.2",
                    DefaultEndpoint = OllamaDefaultEndpoint,
                    Models = []
                }
            });
        }

        [HttpGet("ollama/models")]
        public async Task<ActionResult<OllamaModelsResponse>> GetOllamaModels(CancellationToken ct)
        {
            var config = _providerSettings.Current;
            var baseUrl = config.Provider == LLMProviderType.Ollama && !string.IsNullOrWhiteSpace(config.Endpoint)
                ? config.Endpoint.TrimEnd('/')
                : OllamaDefaultEndpoint;

            var http = _httpClientFactory.CreateClient();
            try
            {
                var response = await http.GetAsync($"{baseUrl}/api/tags", ct);
                if (!response.IsSuccessStatusCode)
                    return Ok(new OllamaModelsResponse { IsOllamaRunning = false });

                var body = await response.Content.ReadFromJsonAsync<OllamaTagsApiResponse>(cancellationToken: ct);
                var modelNames = body?.Models?.Select(m => m.Name).ToList() ?? [];

                return Ok(new OllamaModelsResponse { Models = modelNames, IsOllamaRunning = true });
            }
            catch (HttpRequestException)
            {
                return Ok(new OllamaModelsResponse { IsOllamaRunning = false });
            }
        }

        [HttpGet]
        public ActionResult<LLMSettingsResponse> GetSettings()
        {
            var config = _providerSettings.Current;
            return Ok(new LLMSettingsResponse
            {
                Provider  = config.Provider,
                HasApiKey = !string.IsNullOrEmpty(config.ApiKey),
                Model     = config.Model,
                Endpoint  = config.Endpoint
            });
        }

        [HttpPatch]
        public ActionResult<LLMSettingsResponse> UpdateSettings([FromBody] LLMSettingsRequest request)
        {
            var current = _providerSettings.Current;
            _providerSettings.Current = new LLMProviderConfig
            {
                Provider = request.Provider,
                ApiKey   = !string.IsNullOrWhiteSpace(request.ApiKey) ? request.ApiKey : current.ApiKey,
                Model    = request.Model    ?? current.Model,
                Endpoint = request.Endpoint ?? current.Endpoint
            };

            var updated = _providerSettings.Current;
            return Ok(new LLMSettingsResponse
            {
                Provider  = updated.Provider,
                HasApiKey = !string.IsNullOrEmpty(updated.ApiKey),
                Model     = updated.Model,
                Endpoint  = updated.Endpoint
            });
        }

        private class OllamaTagsApiResponse
        {
            [JsonPropertyName("models")]
            public List<OllamaModelEntry>? Models { get; set; }
        }

        private class OllamaModelEntry
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;
        }
    }
}
