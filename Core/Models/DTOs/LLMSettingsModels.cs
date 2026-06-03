using graphnotelm.Core.Models;

namespace graphnotelm.Core.Models.DTOs
{
    public class LLMSettingsRequest
    {
        public LLMProviderType Provider { get; set; }
        public string? ApiKey { get; set; }
        public string? Model { get; set; }
        public string? Endpoint { get; set; }
    }

    public class LLMSettingsResponse
    {
        public LLMProviderType Provider { get; set; }
        public bool HasApiKey { get; set; }
        public string Model { get; set; } = string.Empty;
        public string? Endpoint { get; set; }
    }

    public class LLMProviderDescriptor
    {
        public LLMProviderType Type { get; set; }
        public bool RequiresApiKey { get; set; }
        public bool RequiresEndpoint { get; set; }
        public string DefaultModel { get; set; } = string.Empty;
        public string? DefaultEndpoint { get; set; }
        public List<string> Models { get; set; } = [];
    }

    public class OllamaModelsResponse
    {
        public List<string> Models { get; set; } = [];
        public bool IsOllamaRunning { get; set; }
    }
}
