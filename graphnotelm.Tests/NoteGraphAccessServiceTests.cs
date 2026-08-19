using graphnotelm.Core.Contexts.Contracts;
using graphnotelm.Core.Models;
using graphnotelm.Core.Services;
using graphnotelm.Infrastructure.Repository.Contracts;
using Moq;

namespace graphnotelm.Tests
{
    public class NoteGraphAccessServiceTests
    {
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _graphId = Guid.NewGuid();

        private readonly Mock<ICurrentUserContext> _currentUserMock = new();
        private readonly Mock<INoteGraphMetadataRepository> _metadataRepoMock = new();
        private readonly Mock<INoteGraphRepository> _graphRepoMock = new();
        private readonly Mock<INoteNodeRepository> _nodeRepoMock = new();

        private readonly NoteGraphAccessService _service;

        public NoteGraphAccessServiceTests()
        {
            _currentUserMock.Setup(c => c.UserId).Returns(_userId);
            _service = new NoteGraphAccessService(
                _currentUserMock.Object,
                _metadataRepoMock.Object,
                _graphRepoMock.Object,
                _nodeRepoMock.Object);
        }

        // ---------- GetAuthorizedMetadataAsync ----------

        [Fact]
        public async Task GetAuthorizedMetadata_NotFound_Fails()
        {
            _metadataRepoMock.Setup(r => r.GetByIdAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((NoteGraphMetadata?)null);

            var result = await _service.GetAuthorizedMetadataAsync(_graphId, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Graph metadata not found", result.Error);
        }

        [Fact]
        public async Task GetAuthorizedMetadata_OtherUsersGraph_Fails()
        {
            _metadataRepoMock.Setup(r => r.GetByIdAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestData.NewMetadata(Guid.NewGuid(), _graphId));

            var result = await _service.GetAuthorizedMetadataAsync(_graphId, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("Access to graph metadata denied", result.Error);
        }

        [Fact]
        public async Task GetAuthorizedMetadata_OwnedGraph_ReturnsMetadata()
        {
            var metadata = TestData.NewMetadata(_userId, _graphId);
            _metadataRepoMock.Setup(r => r.GetByIdAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(metadata);

            var result = await _service.GetAuthorizedMetadataAsync(_graphId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Same(metadata, result.Value);
        }

        // ---------- GetAuthorizedGraphDataAsync ----------

        [Fact]
        public async Task GetAuthorizedGraphData_NotFound_Fails()
        {
            _graphRepoMock.Setup(r => r.GetByIdAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((NoteGraphDocument?)null);

            var result = await _service.GetAuthorizedGraphDataAsync(_graphId, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Full associated graph data not found", result.Error);
        }

        [Fact]
        public async Task GetAuthorizedGraphData_OtherUsersGraph_Fails()
        {
            _graphRepoMock.Setup(r => r.GetByIdAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestData.NewDocument(Guid.NewGuid(), _graphId));

            var result = await _service.GetAuthorizedGraphDataAsync(_graphId, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("denied", result.Error);
        }

        [Fact]
        public async Task GetAuthorizedGraphData_OwnedGraph_ReturnsDocument()
        {
            var document = TestData.NewDocument(_userId, _graphId);
            _graphRepoMock.Setup(r => r.GetByIdAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(document);

            var result = await _service.GetAuthorizedGraphDataAsync(_graphId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Same(document, result.Value);
        }

        // ---------- GetAuthorizedFullDocumentAsync ----------

        [Fact]
        public async Task GetAuthorizedFullDocument_MissingMetadata_Fails()
        {
            _metadataRepoMock.Setup(r => r.GetByIdAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((NoteGraphMetadata?)null);

            var result = await _service.GetAuthorizedFullDocumentAsync(_graphId, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Graph metadata not found", result.Error);
        }

        [Fact]
        public async Task GetAuthorizedFullDocument_LoadsNodesFromNodeRepository()
        {
            var document = TestData.NewDocument(_userId, _graphId);
            var nodeA = TestData.NewNode("A");
            var nodeB = TestData.NewNode("B");

            _metadataRepoMock.Setup(r => r.GetByIdAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestData.NewMetadata(_userId, _graphId));
            _graphRepoMock.Setup(r => r.GetByIdAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(document);
            _nodeRepoMock.Setup(r => r.GetAllByGraphIdAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<NoteNode> { nodeA, nodeB });

            var result = await _service.GetAuthorizedFullDocumentAsync(_graphId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(2, result.Value!.Nodes.Count);
            Assert.Same(nodeA, result.Value.Nodes[nodeA.Id]);
            Assert.Same(nodeB, result.Value.Nodes[nodeB.Id]);
        }
    }
}
