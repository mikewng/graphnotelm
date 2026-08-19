using graphnotelm.Core.Models;
using graphnotelm.Core.Services;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Utils;
using Microsoft.Extensions.AI;
using Moq;

namespace graphnotelm.Tests
{
    public class ChatServiceTests
    {
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _graphId = Guid.NewGuid();

        private readonly Mock<IChatClient> _clientMock = new();
        private readonly Mock<INoteGraphAccessService> _accessMock = new();
        private readonly Mock<IGraphAnalysisService> _graphAnalysisMock = new();

        private readonly ChatService _service;

        public ChatServiceTests()
        {
            var factoryMock = new Mock<ILLMProviderFactory>();
            factoryMock.Setup(f => f.GetClient(It.IsAny<LLMProviderConfig>())).Returns(_clientMock.Object);

            _service = new ChatService(
                factoryMock.Object,
                new LLMProviderSettings(new LLMProviderConfig()),
                _accessMock.Object,
                new GraphToolFactory(_graphAnalysisMock.Object));
        }

        private NoteGraphDocument AuthorizeFullDocument()
        {
            var document = TestData.NewDocument(_userId, _graphId);
            _accessMock.Setup(a => a.GetAuthorizedFullDocumentAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphDocument>.Ok(document));
            return document;
        }

        private async Task<List<AgentEvent>> CollectEventsAsync(IEnumerable<ChatMessage> history)
        {
            var events = new List<AgentEvent>();
            await foreach (var e in _service.RunAsync(_graphId, history))
                events.Add(e);
            return events;
        }

        [Fact]
        public async Task Run_AccessDenied_EmitsErrorAndCompletes()
        {
            _accessMock.Setup(a => a.GetAuthorizedFullDocumentAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphDocument>.Fail("denied"));

            var events = await CollectEventsAsync(new[] { new ChatMessage(ChatRole.User, "hi") });

            Assert.Equal(2, events.Count);
            var delta = Assert.IsType<ContentDelta>(events[0]);
            Assert.Contains("Error", delta.Text);
            Assert.IsType<TurnComplete>(events[1]);
        }

        [Fact]
        public async Task Run_TextOnlyResponse_EmitsContentAndCompletes()
        {
            AuthorizeFullDocument();
            _clientMock
                .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Hello there!")));

            var events = await CollectEventsAsync(new[] { new ChatMessage(ChatRole.User, "hi") });

            Assert.Equal(2, events.Count);
            var delta = Assert.IsType<ContentDelta>(events[0]);
            Assert.Equal("Hello there!", delta.Text);
            Assert.IsType<TurnComplete>(events[1]);
        }

        [Fact]
        public async Task Run_SystemPromptDescribesGraphAndPrependsHistory()
        {
            var document = AuthorizeFullDocument();
            document.Context.SystemPrompt = "Custom graph instructions";
            var node = TestData.NewNode("Photosynthesis");
            document.Nodes[node.Id] = node;
            document.Tags[Guid.NewGuid()] = new TagDefinition { Name = "biology" };

            List<ChatMessage>? sent = null;
            _clientMock
                .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((m, _, _) => sent = m.ToList())
                .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));

            await CollectEventsAsync(new[] { new ChatMessage(ChatRole.User, "what is in my graph?") });

            Assert.NotNull(sent);
            Assert.Equal(ChatRole.System, sent![0].Role);
            Assert.Contains("Custom graph instructions", sent[0].Text);
            Assert.Contains("Photosynthesis", sent[0].Text);
            Assert.Contains("biology", sent[0].Text);
            Assert.Equal("what is in my graph?", sent[1].Text);
        }

        [Fact]
        public async Task Run_PassesGraphToolsToTheModel()
        {
            AuthorizeFullDocument();
            ChatOptions? sentOptions = null;
            _clientMock
                .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((_, o, _) => sentOptions = o)
                .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));

            await CollectEventsAsync(new[] { new ChatMessage(ChatRole.User, "hi") });

            Assert.NotNull(sentOptions);
            Assert.NotNull(sentOptions!.Tools);
            Assert.Equal(8, sentOptions.Tools!.Count);
        }

        [Fact]
        public async Task Run_ToolCall_InvokesToolThenEmitsFinalAnswer()
        {
            var document = AuthorizeFullDocument();
            document.Tags[Guid.NewGuid()] = new TagDefinition { Name = "physics" };

            // Discover the real generated name of the no-argument tag-list tool.
            var toolName = new GraphToolFactory(_graphAnalysisMock.Object)
                .Build(document, new Core.Utils.GraphView(document))
                .Single(t => t.Name.Contains("Tags", StringComparison.OrdinalIgnoreCase)
                          && !t.Name.Contains("Node", StringComparison.OrdinalIgnoreCase))
                .Name;

            var toolCallMessage = new ChatMessage(ChatRole.Assistant,
                new List<AIContent> { new FunctionCallContent("call-1", toolName, new Dictionary<string, object?>()) });

            _clientMock
                .SetupSequence(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChatResponse(toolCallMessage))
                .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Your tags: physics")));

            var events = await CollectEventsAsync(new[] { new ChatMessage(ChatRole.User, "list my tags") });

            var invoked = Assert.Single(events.OfType<ToolInvoked>());
            Assert.Equal(toolName, invoked.ToolName);
            var toolResult = Assert.Single(events.OfType<ToolResult>());
            Assert.Equal(toolName, toolResult.ToolName);
            var delta = Assert.Single(events.OfType<ContentDelta>());
            Assert.Equal("Your tags: physics", delta.Text);
            Assert.IsType<TurnComplete>(events.Last());
        }
    }
}
