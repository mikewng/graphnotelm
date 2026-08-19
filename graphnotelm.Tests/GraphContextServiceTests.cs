using graphnotelm.Core.Models;
using graphnotelm.Core.Services;
using graphnotelm.Core.Services.Contracts;
using Microsoft.Extensions.AI;
using Moq;

namespace graphnotelm.Tests
{
    public class GraphContextServiceTests
    {
        private readonly Mock<IChatClient> _clientMock = new();
        private readonly GraphContextService _service;

        private List<ChatMessage>? _sentMessages;

        public GraphContextServiceTests()
        {
            var factoryMock = new Mock<ILLMProviderFactory>();
            factoryMock.Setup(f => f.GetClient(It.IsAny<LLMProviderConfig>())).Returns(_clientMock.Object);

            _service = new GraphContextService(
                factoryMock.Object,
                new LLMProviderSettings(new LLMProviderConfig()));
        }

        private void SetupResponse(string text)
        {
            _clientMock
                .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((m, _, _) => _sentMessages = m.ToList())
                .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
        }

        [Fact]
        public async Task InferContext_DeserializesLLMResponseIntoGraphContext()
        {
            SetupResponse("{\"SystemPrompt\":\"Tracks cooking recipes\",\"MetadataSchemaHint\":\"summary, cuisine\"}");

            var result = await _service.InferContextAsync(TestData.NewDocument(Guid.NewGuid()));

            Assert.True(result.Success);
            Assert.Equal("Tracks cooking recipes", result.Value!.SystemPrompt);
            Assert.Equal("summary, cuisine", result.Value.MetadataSchemaHint);
        }

        [Fact]
        public async Task InferContext_MapsCamelCaseKeysFromPrompt()
        {
            // The prompt instructs the LLM to answer with camelCase keys
            SetupResponse("{\"domain\":\"cooking\",\"systemPrompt\":\"Tracks recipes\",\"metadataSchemaHint\":\"summary, cuisine\"}");

            var result = await _service.InferContextAsync(TestData.NewDocument(Guid.NewGuid()));

            Assert.True(result.Success);
            Assert.Equal("Tracks recipes", result.Value!.SystemPrompt);
            Assert.Equal("summary, cuisine", result.Value.MetadataSchemaHint);
        }

        [Fact]
        public async Task InferContext_StripsMarkdownFences()
        {
            SetupResponse("```json\n{\"systemPrompt\":\"Fenced\",\"metadataSchemaHint\":\"summary\"}\n```");

            var result = await _service.InferContextAsync(TestData.NewDocument(Guid.NewGuid()));

            Assert.True(result.Success);
            Assert.Equal("Fenced", result.Value!.SystemPrompt);
        }

        [Fact]
        public async Task InferContext_InvalidJson_FailsInsteadOfThrowing()
        {
            SetupResponse("Sorry, I cannot produce JSON right now.");

            var result = await _service.InferContextAsync(TestData.NewDocument(Guid.NewGuid()));

            Assert.False(result.Success);
            Assert.Equal("LLM returned invalid JSON.", result.Error);
        }

        [Fact]
        public async Task InferContext_ProviderHttpError_Fails()
        {
            _clientMock
                .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("boom"));

            var result = await _service.InferContextAsync(TestData.NewDocument(Guid.NewGuid()));

            Assert.False(result.Success);
            Assert.Contains("AI provider error", result.Error);
        }

        [Fact]
        public async Task InferContext_NullJson_FallsBackToDefaultContext()
        {
            SetupResponse("null");

            var result = await _service.InferContextAsync(TestData.NewDocument(Guid.NewGuid()));

            Assert.True(result.Success);
            Assert.Equal("", result.Value!.SystemPrompt);
            Assert.Equal("summary", result.Value.MetadataSchemaHint);
        }

        [Fact]
        public async Task InferContext_PromptIncludesSampleNodesAndRelationshipTypes()
        {
            var document = TestData.NewDocument(Guid.NewGuid());
            var relId = Guid.NewGuid();
            document.Relationships[relId] = new RelationshipDefinition { Name = "requires", Inverse = "required by" };
            var target = TestData.NewNode("Flour");
            var source = TestData.NewNode("Bread", "How to bake bread");
            source.Relationships.Add(new NodeRelationship { TargetNodeId = target.Id, RelationshipId = relId });
            document.Nodes[source.Id] = source;
            document.Nodes[target.Id] = target;

            SetupResponse("null");
            await _service.InferContextAsync(document);

            Assert.NotNull(_sentMessages);
            var userPrompt = _sentMessages!.Last().Text;
            Assert.Contains("Bread", userPrompt);
            Assert.Contains("requires", userPrompt);
            Assert.Contains("Flour", userPrompt);
        }

        [Fact]
        public async Task InferContext_UsesCustomSystemPromptWhenPresent()
        {
            var document = TestData.NewDocument(Guid.NewGuid());
            document.Context.SystemPrompt = "MY CUSTOM ANALYSIS INSTRUCTIONS";

            SetupResponse("null");
            await _service.InferContextAsync(document);

            var userPrompt = _sentMessages!.Last().Text;
            Assert.Contains("MY CUSTOM ANALYSIS INSTRUCTIONS", userPrompt);
        }

        [Fact]
        public async Task InferContext_TruncatesLongNotesInSample()
        {
            var document = TestData.NewDocument(Guid.NewGuid());
            var longNote = new string('a', 500);
            var node = TestData.NewNode("Long", longNote);
            document.Nodes[node.Id] = node;

            SetupResponse("null");
            await _service.InferContextAsync(document);

            var userPrompt = _sentMessages!.Last().Text;
            Assert.DoesNotContain(longNote, userPrompt);
            Assert.Contains(new string('a', 200), userPrompt);
        }
    }
}
