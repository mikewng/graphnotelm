using graphnotelm.Core.Contexts.Contracts;
using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure.Contracts;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;
using Moq;

namespace graphnotelm.Tests
{
    public class NoteGraphServiceTests
    {
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _graphId = Guid.NewGuid();

        private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
        private readonly Mock<ICurrentUserContext> _currentUserMock = new();
        private readonly Mock<INoteGraphMetadataRepository> _metadataRepoMock = new();
        private readonly Mock<INoteGraphRepository> _graphRepoMock = new();
        private readonly Mock<INoteGraphAccessService> _accessMock = new();
        private readonly Mock<INoteNodeRepository> _nodeRepoMock = new();
        private readonly Mock<ILLMAnalysisService> _llmAnalysisMock = new();

        private readonly NoteGraphService _service;

        public NoteGraphServiceTests()
        {
            _currentUserMock.Setup(c => c.UserId).Returns(_userId);

            _service = new NoteGraphService(
                _unitOfWorkMock.Object,
                _currentUserMock.Object,
                _metadataRepoMock.Object,
                _graphRepoMock.Object,
                _accessMock.Object,
                _nodeRepoMock.Object,
                _llmAnalysisMock.Object);
        }

        private NoteGraphDocument AuthorizeFullDocument()
        {
            var document = TestData.NewDocument(_userId, _graphId);
            TestData.WireDocument(_accessMock, _nodeRepoMock, document, _userId);
            return document;
        }

        private NoteGraphMetadata AuthorizeMetadata()
        {
            var metadata = TestData.NewMetadata(_userId, _graphId);
            _accessMock.Setup(a => a.GetAuthorizedMetadataAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphMetadata>.Ok(metadata));
            return metadata;
        }

        // ---------- GetNoteGraphById ----------

        [Fact]
        public async Task GetNoteGraphById_AccessDenied_Fails()
        {
            _accessMock.Setup(a => a.GetAuthorizedSkeletonDocumentAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphDocument>.Fail("denied"));

            var result = await _service.GetNoteGraphById(_graphId, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("denied", result.Error);
        }

        [Fact]
        public async Task GetNoteGraphById_ReturnsSkeletonWithoutNoteBodies()
        {
            var document = AuthorizeFullDocument();
            document.Context.SystemPrompt = "prompt";
            var node = TestData.NewNode("Title A", "secret note body");
            node.Metadata.IsPinned = true;
            node.Metadata.UserConfidenceRate = 0.5f;
            document.Nodes[node.Id] = node;

            var result = await _service.GetNoteGraphById(_graphId, CancellationToken.None);

            Assert.True(result.Success);
            var skeleton = result.Value!;
            Assert.Equal(document.Id, skeleton.Id);
            Assert.Equal("prompt", skeleton.SystemPrompt);
            var nodeSkeleton = skeleton.Nodes[node.Id];
            Assert.Equal("Title A", nodeSkeleton.Title);
            Assert.True(nodeSkeleton.Metadata.IsPinned);
            Assert.Equal(0.5f, nodeSkeleton.Metadata.UserConfidenceRate);
        }

        // ---------- GetNoteGraphList ----------

        [Fact]
        public async Task GetNoteGraphList_NoGraphs_Fails()
        {
            _metadataRepoMock.Setup(r => r.GetListByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<NoteGraphMetadata>());

            var result = await _service.GetNoteGraphList(CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("No graphs associated with user ID.", result.Error);
        }

        [Fact]
        public async Task GetNoteGraphList_NullFromRepository_Fails()
        {
            _metadataRepoMock.Setup(r => r.GetListByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((List<NoteGraphMetadata>)null!);

            var result = await _service.GetNoteGraphList(CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("List returned as null.", result.Error);
        }

        [Fact]
        public async Task GetNoteGraphList_ReturnsUsersGraphs()
        {
            var list = new List<NoteGraphMetadata> { TestData.NewMetadata(_userId), TestData.NewMetadata(_userId) };
            _metadataRepoMock.Setup(r => r.GetListByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(list);

            var result = await _service.GetNoteGraphList(CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(list, result.Value!.GraphList);
        }

        // ---------- GetArchivedNoteGraphList ----------

        [Fact]
        public async Task GetArchivedNoteGraphList_ReturnsDeletedGraphs()
        {
            var list = new List<NoteGraphMetadata> { TestData.NewMetadata(_userId) };
            _metadataRepoMock.Setup(r => r.GetDeletedListByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(list);

            var result = await _service.GetArchivedNoteGraphList(CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(list, result.Value!.GraphList);
        }

        // ---------- CreateNoteGraph ----------

        [Fact]
        public async Task CreateNoteGraph_EmptyName_Fails()
        {
            var result = await _service.CreateNoteGraph(
                new CreateGraphRequest { Name = "" }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Failed to create: Name was empty.", result.Error);
        }

        [Fact]
        public async Task CreateNoteGraph_Valid_SavesMetadataAndDocument()
        {
            NoteGraphMetadata? savedMetadata = null;
            NoteGraphDocument? savedDocument = null;
            _metadataRepoMock.Setup(r => r.AddAsync(It.IsAny<NoteGraphMetadata>(), It.IsAny<CancellationToken>()))
                .Callback<NoteGraphMetadata, CancellationToken>((m, _) => savedMetadata = m)
                .Returns(Task.CompletedTask);
            _graphRepoMock.Setup(r => r.SaveAsync(It.IsAny<NoteGraphDocument>()))
                .Callback<NoteGraphDocument>(d => savedDocument = d)
                .Returns(Task.CompletedTask);

            var result = await _service.CreateNoteGraph(
                new CreateGraphRequest { Name = "New Graph", Description = "desc" }, CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(result.Value!.IsSuccess);
            Assert.NotNull(savedMetadata);
            Assert.Equal("New Graph", savedMetadata!.Name);
            Assert.Equal(_userId, savedMetadata.UserId);
            Assert.NotNull(savedDocument);
            Assert.Equal(savedMetadata.Id, savedDocument!.Id);
            Assert.Equal(result.Value.Id, savedMetadata.Id);
        }

        [Fact]
        public async Task CreateNoteGraph_RepositoryThrows_Fails()
        {
            _metadataRepoMock.Setup(r => r.AddAsync(It.IsAny<NoteGraphMetadata>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("db down"));

            var result = await _service.CreateNoteGraph(
                new CreateGraphRequest { Name = "New Graph" }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Failed to create new graph.", result.Error);
        }

        // ---------- EditGraphMetadataById ----------

        [Fact]
        public async Task EditGraphMetadata_AccessDenied_Fails()
        {
            _accessMock.Setup(a => a.GetAuthorizedMetadataAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphMetadata>.Fail("denied"));

            var result = await _service.EditGraphMetadataById(
                new EditGraphMetadataRequest { Name = "x", Description = "y" }, _graphId, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("denied", result.Error);
        }

        [Fact]
        public async Task EditGraphMetadata_NullFields_KeepExistingValues()
        {
            var metadata = AuthorizeMetadata();
            metadata.Name = "Original";
            metadata.Description = "Original description";
            _metadataRepoMock.Setup(r => r.UpdateAsync(metadata, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var result = await _service.EditGraphMetadataById(
                new EditGraphMetadataRequest { Name = null!, Description = "updated" }, _graphId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("Original", result.Value!.Name);
            Assert.Equal("updated", result.Value.Description);
        }

        // ---------- EditGraphContextById ----------

        [Fact]
        public async Task EditGraphContext_UpdatesSystemPromptAndSaves()
        {
            var document = AuthorizeFullDocument();
            _graphRepoMock.Setup(r => r.SaveAsync(document)).Returns(Task.CompletedTask);

            var result = await _service.EditGraphContextById(
                new EditGraphContextRequest { SystemPrompt = "new prompt" }, _graphId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("new prompt", document.Context.SystemPrompt);
            _graphRepoMock.Verify(r => r.SaveAsync(document), Times.Once);
        }

        // ---------- DeleteNoteGraphById (soft delete) ----------

        [Fact]
        public async Task DeleteNoteGraph_MarksMetadataDeleted()
        {
            var metadata = AuthorizeMetadata();
            _metadataRepoMock.Setup(r => r.UpdateAsync(metadata, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var result = await _service.DeleteNoteGraphById(_graphId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(result.Value!.isDeleted);
            Assert.True(metadata.IsDeleted);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        // ---------- HardDeleteNoteGraphById ----------

        [Fact]
        public async Task HardDelete_ArchivedGraphNotFound_Fails()
        {
            _metadataRepoMock.Setup(r => r.GetDeletedByIdAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((NoteGraphMetadata?)null);

            var result = await _service.HardDeleteNoteGraphById(_graphId, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Archived graph not found.", result.Error);
        }

        [Fact]
        public async Task HardDelete_OtherUsersGraph_Fails()
        {
            _metadataRepoMock.Setup(r => r.GetDeletedByIdAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestData.NewMetadata(Guid.NewGuid(), _graphId));

            var result = await _service.HardDeleteNoteGraphById(_graphId, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Access denied.", result.Error);
        }

        [Fact]
        public async Task HardDelete_DeletesNodesDocumentAndMetadata()
        {
            var nodeA = TestData.NewNode("A");
            var nodeB = TestData.NewNode("B");
            _metadataRepoMock.Setup(r => r.GetDeletedByIdAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestData.NewMetadata(_userId, _graphId));
            _nodeRepoMock.Setup(r => r.GetAllByGraphIdAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<NoteNode> { nodeA, nodeB });
            _metadataRepoMock.Setup(r => r.DeleteAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var result = await _service.HardDeleteNoteGraphById(_graphId, CancellationToken.None);

            Assert.True(result.Success);
            _nodeRepoMock.Verify(r => r.DeleteAsync(_graphId, nodeA.Id), Times.Once);
            _nodeRepoMock.Verify(r => r.DeleteAsync(_graphId, nodeB.Id), Times.Once);
            _graphRepoMock.Verify(r => r.DeleteByIdAsync(_graphId), Times.Once);
            _metadataRepoMock.Verify(r => r.DeleteAsync(_graphId, It.IsAny<CancellationToken>()), Times.Once);
        }

        // ---------- UnarchiveNoteGraphById ----------

        [Fact]
        public async Task Unarchive_RestoresGraph()
        {
            var metadata = TestData.NewMetadata(_userId, _graphId);
            metadata.IsDeleted = true;
            _metadataRepoMock.Setup(r => r.GetDeletedByIdAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(metadata);
            _metadataRepoMock.Setup(r => r.UpdateAsync(metadata, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var result = await _service.UnarchiveNoteGraphById(_graphId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.False(result.Value!.isDeleted);
            Assert.False(metadata.IsDeleted);
        }

        // ---------- CreateNoteGraphFromText ----------

        [Fact]
        public async Task CreateNoteGraphFromText_EmptyContent_Fails()
        {
            var result = await _service.CreateNoteGraphFromText(
                new CreateGraphFromTextRequest { Content = "   " }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Content was empty.", result.Error);
        }

        [Fact]
        public async Task CreateNoteGraphFromText_ExtractionFails_Fails()
        {
            _llmAnalysisMock.Setup(l => l.ExtractGraphFromTextAsync("name", "content", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphDocumentREADONLY>.Fail("llm error"));

            var result = await _service.CreateNoteGraphFromText(
                new CreateGraphFromTextRequest { Name = "name", Content = "content" }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("llm error", result.Error);
        }

        [Fact]
        public async Task CreateNoteGraphFromText_ExtractionSucceeds_ImportsGraph()
        {
            var node = TestData.NewNode("Extracted");
            var extracted = new NoteGraphDocumentREADONLY
            {
                Name = "Extracted Graph",
                Nodes = new Dictionary<Guid, NoteNode> { [node.Id] = node }
            };
            _llmAnalysisMock.Setup(l => l.ExtractGraphFromTextAsync(null, "content", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphDocumentREADONLY>.Ok(extracted));

            var result = await _service.CreateNoteGraphFromText(
                new CreateGraphFromTextRequest { Content = "content" }, CancellationToken.None);

            Assert.True(result.Success);
            _nodeRepoMock.Verify(r => r.SaveAsync(It.IsAny<Guid>(), node), Times.Once);
        }

        // ---------- ImportNoteGraphFromJSON ----------

        [Fact]
        public async Task ImportNoteGraph_EmptyName_Fails()
        {
            var result = await _service.ImportNoteGraphFromJSON(
                new NoteGraphDocumentREADONLY { Name = "" }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Failed to import: Name was empty.", result.Error);
        }

        [Fact]
        public async Task ImportNoteGraph_SavesMetadataDocumentAndNodes()
        {
            var node = TestData.NewNode("Imported");
            var tagId = Guid.NewGuid();
            var import = new NoteGraphDocumentREADONLY
            {
                Name = "Imported Graph",
                Tags = new Dictionary<Guid, TagDefinition> { [tagId] = new TagDefinition { Name = "tag" } },
                Nodes = new Dictionary<Guid, NoteNode> { [node.Id] = node }
            };

            NoteGraphDocument? savedDocument = null;
            _graphRepoMock.Setup(r => r.SaveAsync(It.IsAny<NoteGraphDocument>()))
                .Callback<NoteGraphDocument>(d => savedDocument = d)
                .Returns(Task.CompletedTask);

            var result = await _service.ImportNoteGraphFromJSON(import, CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(savedDocument);
            Assert.Equal(_userId, savedDocument!.UserId);
            Assert.True(savedDocument.Tags.ContainsKey(tagId));
            _nodeRepoMock.Verify(r => r.SaveAsync(savedDocument.Id, node), Times.Once);
        }

        // ---------- ExportNoteGraphAsJSON ----------

        [Fact]
        public async Task ExportNoteGraph_ReturnsDocumentWithNodes()
        {
            var metadata = AuthorizeMetadata();
            metadata.Name = "Export Me";
            var document = TestData.NewDocument(_userId, _graphId);
            _accessMock.Setup(a => a.GetAuthorizedGraphDataAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphDocument>.Ok(document));
            var node = TestData.NewNode("Node 1");
            _nodeRepoMock.Setup(r => r.GetAllByGraphIdAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<NoteNode> { node });

            var result = await _service.ExportNoteGraphAsJSON(_graphId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("Export Me", result.Value!.Name);
            Assert.Same(node, result.Value.Nodes[node.Id]);
        }
    }
}
