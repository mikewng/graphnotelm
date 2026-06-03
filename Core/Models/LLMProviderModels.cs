namespace graphnotelm.Core.Models
{
    public enum LLMProviderType { Anthropic, OpenAI, Local, Ollama }

    public class LLMProviderConfig
    {
        public LLMProviderType Provider { get; set; } = LLMProviderType.Anthropic;
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? Endpoint { get; set; }
    }

    public class LLMProviderSettings
    {
        private LLMProviderConfig _current;
        private readonly object _lock = new();

        public LLMProviderSettings(LLMProviderConfig initial)
        {
            _current = initial;
        }

        public LLMProviderConfig Current
        {
            get { lock (_lock) return _current; }
            set { lock (_lock) _current = value; }
        }
    }
}
