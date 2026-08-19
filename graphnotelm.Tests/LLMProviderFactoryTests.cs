using graphnotelm.Core.Models;
using graphnotelm.Core.Services;

namespace graphnotelm.Tests
{
    public class LLMProviderFactoryTests
    {
        private readonly LLMProviderFactory _factory = new();

        [Theory]
        [InlineData(LLMProviderType.Anthropic)]
        [InlineData(LLMProviderType.OpenAI)]
        [InlineData(LLMProviderType.Ollama)]
        public void GetClient_KnownProvider_ReturnsClient(LLMProviderType provider)
        {
            var client = _factory.GetClient(new LLMProviderConfig
            {
                Provider = provider,
                ApiKey = "test-key",
                Model = "test-model"
            });

            Assert.NotNull(client);
        }

        [Fact]
        public void GetClient_LocalProvider_RequiresEndpoint()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => _factory.GetClient(new LLMProviderConfig
            {
                Provider = LLMProviderType.Local,
                Endpoint = null
            }));

            Assert.Contains("Endpoint", ex.Message);
        }

        [Fact]
        public void GetClient_LocalProviderWithEndpoint_ReturnsClient()
        {
            var client = _factory.GetClient(new LLMProviderConfig
            {
                Provider = LLMProviderType.Local,
                Endpoint = "http://localhost:8080"
            });

            Assert.NotNull(client);
        }

        [Fact]
        public void GetClient_UnknownProvider_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _factory.GetClient(new LLMProviderConfig
            {
                Provider = (LLMProviderType)999
            }));
        }

        [Fact]
        public void GetClient_SameConfig_ReturnsCachedInstance()
        {
            var config = new LLMProviderConfig
            {
                Provider = LLMProviderType.Anthropic,
                ApiKey = "key",
                Model = "model"
            };

            var first = _factory.GetClient(config);
            var second = _factory.GetClient(new LLMProviderConfig
            {
                Provider = LLMProviderType.Anthropic,
                ApiKey = "key",
                Model = "model"
            });

            Assert.Same(first, second);
        }

        [Fact]
        public void GetClient_DifferentConfig_ReturnsDistinctInstances()
        {
            var first = _factory.GetClient(new LLMProviderConfig
            {
                Provider = LLMProviderType.Anthropic,
                ApiKey = "key-1"
            });
            var second = _factory.GetClient(new LLMProviderConfig
            {
                Provider = LLMProviderType.Anthropic,
                ApiKey = "key-2"
            });

            Assert.NotSame(first, second);
        }
    }
}
