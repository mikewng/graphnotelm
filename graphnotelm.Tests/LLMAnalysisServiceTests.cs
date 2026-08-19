using graphnotelm.Core.Models;
using graphnotelm.Core.Services;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Core.Utils;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;
using Microsoft.Extensions.AI;
using Moq;
using System.Text.Json;

namespace graphnotelm.Tests
{
    public class LLMAnalysisServiceTests
    {
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _graphId = Guid.NewGuid();

        private readonly Mock<IChatClient> _clientMock = new();
        private readonly Mock<ILLMContextBuilder> _contextBuilderMock = new();
        private readonly Mock<IGraphAnalysisService> _graphAnalysisMock = new();
        private readonly Mock<INoteGraphAccessService> _accessMock = new();
        private readonly Mock<INoteNodeRepository> _nodeRepoMock = new();

        private readonly LLMAnalysisService _service;

        public LLMAnalysisServiceTests()
        {
            var factoryMock = new Mock<ILLMProviderFactory>();
            factoryMock.Setup(f => f.GetClient(It.IsAny<LLMProviderConfig>())).Returns(_clientMock.Object);

            _service = new LLMAnalysisService(
                factoryMock.Object,
                new LLMProviderSettings(new LLMProviderConfig()),
                _contextBuilderMock.Object,
                _graphAnalysisMock.Object,
                _accessMock.Object,
                _nodeRepoMock.Object);
        }

        private NoteGraphDocument AuthorizeFullDocument()
        {
            var document = TestData.NewDocument(_userId, _graphId);
            _accessMock.Setup(a => a.GetAuthorizedFullDocumentAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphDocument>.Ok(document));
            return document;
        }

        private void SetupClientResponse(string text)
        {
            _clientMock
                .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
        }

        private NoteNode SetupAnalyzableNode(NoteGraphDocument document)
        {
            var node = TestData.NewNode("Analyze Me");
            document.Nodes[node.Id] = node;
            _graphAnalysisMock.Setup(g => g.BuildView(document, node.Id)).Returns(new GraphView(document));
            _contextBuilderMock.Setup(b => b.BuildNodeAnalysisPrompt(document, It.IsAny<GraphView>(), node.Id))
                .Returns(new LLMPrompt { System = "sys", User = "user" });
            return node;
        }

        // ---------- AnalyzeNodeAsync ----------

        [Fact]
        public async Task AnalyzeNode_AccessDenied_Fails()
        {
            _accessMock.Setup(a => a.GetAuthorizedFullDocumentAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphDocument>.Fail("denied"));

            var result = await _service.AnalyzeNodeAsync(_graphId, Guid.NewGuid(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("denied", result.Error);
        }

        [Fact]
        public async Task AnalyzeNode_NodeMissing_Fails()
        {
            AuthorizeFullDocument();

            var result = await _service.AnalyzeNodeAsync(_graphId, Guid.NewGuid(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Node not found.", result.Error);
        }

        [Fact]
        public async Task AnalyzeNode_StripsMarkdownFencesAndStoresLlmNamespace()
        {
            var document = AuthorizeFullDocument();
            var node = SetupAnalyzableNode(document);
            SetupClientResponse("```json\n{\"summary\":\"a fine node\"}\n```");

            var result = await _service.AnalyzeNodeAsync(_graphId, node.Id, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(node.Id, result.Value!.NodeId);
            var stored = node.Metadata.GetRawNamespace("llm");
            Assert.NotNull(stored);
            Assert.Equal("a fine node", stored!.Value.GetProperty("summary").GetString());
            _nodeRepoMock.Verify(r => r.SaveAsync(_graphId, node), Times.Once);
        }

        [Fact]
        public async Task AnalyzeNode_UnwrapsResponseNestedUnderLlmKey()
        {
            var document = AuthorizeFullDocument();
            var node = SetupAnalyzableNode(document);
            SetupClientResponse("{\"llm\":{\"summary\":\"wrapped\"}}");

            var result = await _service.AnalyzeNodeAsync(_graphId, node.Id, CancellationToken.None);

            Assert.True(result.Success);
            var stored = node.Metadata.GetRawNamespace("llm");
            Assert.Equal("wrapped", stored!.Value.GetProperty("summary").GetString());
        }

        [Fact]
        public async Task AnalyzeNode_InvalidJson_Fails()
        {
            var document = AuthorizeFullDocument();
            var node = SetupAnalyzableNode(document);
            SetupClientResponse("this is not json");

            var result = await _service.AnalyzeNodeAsync(_graphId, node.Id, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("LLM returned invalid JSON.", result.Error);
            _nodeRepoMock.Verify(r => r.SaveAsync(It.IsAny<Guid>(), It.IsAny<NoteNode>()), Times.Never);
        }

        [Fact]
        public async Task AnalyzeNode_ProviderHttpError_Fails()
        {
            var document = AuthorizeFullDocument();
            var node = SetupAnalyzableNode(document);
            _clientMock
                .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("boom"));

            var result = await _service.AnalyzeNodeAsync(_graphId, node.Id, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("AI provider error", result.Error);
        }

        // ---------- ExtractGraphFromTextAsync ----------

        private void SetupExtractionPrompts(string content)
        {
            _contextBuilderMock.Setup(b => b.BuildGraphExtractionPass1Prompt(content))
                .Returns(new LLMPrompt { System = "PASS1", User = content });
            _contextBuilderMock.Setup(b => b.BuildGraphExtractionPass2Prompt(
                    content, It.IsAny<Dictionary<Guid, NoteNode>>(), It.IsAny<Dictionary<Guid, RelationshipDefinition>>()))
                .Returns(new LLMPrompt { System = "PASS2", User = "pass2" });
        }

        private void SetupPassResponse(string passMarker, Func<string> textFactory)
        {
            _clientMock
                .Setup(c => c.GetResponseAsync(
                    It.Is<IEnumerable<ChatMessage>>(m => m.First().Text == passMarker),
                    It.IsAny<ChatOptions?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ChatResponse(new ChatMessage(ChatRole.Assistant, textFactory())));
        }

        [Fact]
        public async Task ExtractGraph_Pass1InvalidJson_Fails()
        {
            SetupExtractionPrompts("content");
            SetupPassResponse("PASS1", () => "not json");

            var result = await _service.ExtractGraphFromTextAsync("name", "content", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("LLM returned invalid JSON on pass 1.", result.Error);
        }

        [Fact]
        public async Task ExtractGraph_NoNodesExtracted_Fails()
        {
            SetupExtractionPrompts("content");
            SetupPassResponse("PASS1", () => "{\"graphName\":\"g\",\"nodes\":[],\"tags\":[],\"relationshipTypes\":[]}");

            var result = await _service.ExtractGraphFromTextAsync("name", "content", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("LLM did not extract any nodes from the document.", result.Error);
        }

        [Fact]
        public async Task ExtractGraph_BuildsNodesTagsAndWiresRelationships()
        {
            const string pass1Json = """
                {
                  "graphName": "Extracted",
                  "nodes": [
                    { "title": "A", "note": "note a", "tags": ["Concept"] },
                    { "title": "B", "note": "note b", "tags": ["Bogus Tag"] }
                  ],
                  "tags": [ { "name": "Concept" } ],
                  "relationshipTypes": [ { "name": "causes", "inverse": "caused by" } ]
                }
                """;

            Dictionary<Guid, NoteNode>? capturedNodes = null;
            Dictionary<Guid, RelationshipDefinition>? capturedRelTypes = null;
            _contextBuilderMock.Setup(b => b.BuildGraphExtractionPass1Prompt("content"))
                .Returns(new LLMPrompt { System = "PASS1", User = "content" });
            _contextBuilderMock.Setup(b => b.BuildGraphExtractionPass2Prompt(
                    "content", It.IsAny<Dictionary<Guid, NoteNode>>(), It.IsAny<Dictionary<Guid, RelationshipDefinition>>()))
                .Callback<string, Dictionary<Guid, NoteNode>, Dictionary<Guid, RelationshipDefinition>>(
                    (_, nodes, relTypes) => { capturedNodes = nodes; capturedRelTypes = relTypes; })
                .Returns(new LLMPrompt { System = "PASS2", User = "pass2" });

            SetupPassResponse("PASS1", () => pass1Json);
            SetupPassResponse("PASS2", () =>
            {
                var nodeA = capturedNodes!.Values.First(n => n.Title == "A");
                var nodeB = capturedNodes!.Values.First(n => n.Title == "B");
                var relId = capturedRelTypes!.Keys.Single();
                return JsonSerializer.Serialize(new
                {
                    relationships = new[]
                    {
                        new { sourceNodeId = nodeA.Id, targetNodeId = nodeB.Id, relationshipId = relId },
                        // self-relationship must be discarded
                        new { sourceNodeId = nodeA.Id, targetNodeId = nodeA.Id, relationshipId = relId }
                    }
                });
            });

            var result = await _service.ExtractGraphFromTextAsync(null, "content", CancellationToken.None);

            Assert.True(result.Success);
            var graph = result.Value!;
            Assert.Equal("Extracted", graph.Name); // falls back to LLM-provided graphName
            Assert.Equal(2, graph.Nodes.Count);
            Assert.Single(graph.Tags);
            Assert.Single(graph.Relationships);

            var a = graph.Nodes.Values.First(n => n.Title == "A");
            var b = graph.Nodes.Values.First(n => n.Title == "B");
            Assert.Single(a.Tags);           // "Concept" resolved
            Assert.Empty(b.Tags);            // "Bogus Tag" not defined => dropped
            var edge = Assert.Single(a.Relationships);
            Assert.Equal(b.Id, edge.TargetNodeId);
        }

        [Fact]
        public async Task ExtractGraph_Pass2InvalidJson_StillReturnsGraphWithoutRelationships()
        {
            SetupExtractionPrompts("content");
            SetupPassResponse("PASS1",
                () => "{\"graphName\":\"g\",\"nodes\":[{\"title\":\"A\",\"note\":\"n\",\"tags\":[]}],\"tags\":[],\"relationshipTypes\":[]}");
            SetupPassResponse("PASS2", () => "garbage");

            var result = await _service.ExtractGraphFromTextAsync("Chosen Name", "content", CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("Chosen Name", result.Value!.Name); // explicit name wins over graphName
            var node = Assert.Single(result.Value.Nodes.Values);
            Assert.Empty(node.Relationships);
        }

        // ---------- ExtractNodeFromPasteAsync ----------

        [Fact]
        public async Task ExtractNodeFromPaste_AccessDenied_Fails()
        {
            _accessMock.Setup(a => a.GetAuthorizedFullDocumentAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphDocument>.Fail("denied"));

            var result = await _service.ExtractNodeFromPasteAsync(_graphId, "pasted", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("denied", result.Error);
        }

        [Fact]
        public async Task ExtractNodeFromPaste_MissingTitle_Fails()
        {
            var document = AuthorizeFullDocument();
            _contextBuilderMock.Setup(b => b.BuildNodeFromPastePrompt(document, "pasted"))
                .Returns(new LLMPrompt { System = "s", User = "u" });
            SetupClientResponse("{\"title\":\"\",\"note\":\"n\"}");

            var result = await _service.ExtractNodeFromPasteAsync(_graphId, "pasted", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("LLM did not return a valid title.", result.Error);
        }

        [Fact]
        public async Task ExtractNodeFromPaste_FiltersUnknownTagAndRelationshipGuids()
        {
            var document = AuthorizeFullDocument();
            var knownTagId = Guid.NewGuid();
            var knownRelId = Guid.NewGuid();
            var knownTarget = TestData.NewNode("Known Target");
            document.Tags[knownTagId] = new TagDefinition { Name = "tag" };
            document.Relationships[knownRelId] = new RelationshipDefinition { Name = "rel" };
            document.Nodes[knownTarget.Id] = knownTarget;

            _contextBuilderMock.Setup(b => b.BuildNodeFromPastePrompt(document, "pasted"))
                .Returns(new LLMPrompt { System = "s", User = "u" });

            var responseJson = JsonSerializer.Serialize(new
            {
                title = "Extracted Title",
                note = "extracted note",
                tags = new[] { knownTagId.ToString(), Guid.NewGuid().ToString() },
                relationships = new[]
                {
                    new { targetNodeId = knownTarget.Id.ToString(), relationshipId = knownRelId.ToString() },
                    new { targetNodeId = Guid.NewGuid().ToString(), relationshipId = knownRelId.ToString() }
                }
            });
            SetupClientResponse(responseJson);

            var result = await _service.ExtractNodeFromPasteAsync(_graphId, "pasted", CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("Extracted Title", result.Value!.Title);
            Assert.Equal("extracted note", result.Value.Note);
            Assert.Equal(new List<Guid> { knownTagId }, result.Value.Tags);
            var rel = Assert.Single(result.Value.Relationships);
            Assert.Equal(knownTarget.Id, rel.TargetNodeId);
        }

        [Fact]
        public async Task ExtractNodeFromPaste_InvalidJson_Fails()
        {
            var document = AuthorizeFullDocument();
            _contextBuilderMock.Setup(b => b.BuildNodeFromPastePrompt(document, "pasted"))
                .Returns(new LLMPrompt { System = "s", User = "u" });
            SetupClientResponse("nope");

            var result = await _service.ExtractNodeFromPasteAsync(_graphId, "pasted", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("LLM returned invalid JSON.", result.Error);
        }
    }
}
