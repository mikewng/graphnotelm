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
    public class NoteGraphFolderServiceTests
    {
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _graphId = Guid.NewGuid();

        private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
        private readonly Mock<INoteGraphAccessService> _accessMock = new();
        private readonly Mock<INoteGraphRepository> _graphRepoMock = new();
        private readonly Mock<INoteNodeRepository> _nodeRepoMock = new();

        private readonly NoteGraphFolderService _service;

        public NoteGraphFolderServiceTests()
        {
            _service = new NoteGraphFolderService(
                _unitOfWorkMock.Object,
                _accessMock.Object,
                _graphRepoMock.Object,
                _nodeRepoMock.Object);
        }

        private NoteGraphDocument AuthorizeFullDocument()
        {
            var document = TestData.NewDocument(_userId, _graphId);
            TestData.WireDocument(_accessMock, _nodeRepoMock, document, _userId);
            return document;
        }

        [Fact]
        public async Task GetFolderList_AccessDenied_Fails()
        {
            _accessMock.Setup(a => a.GetAuthorizedGraphDataAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphDocument>.Fail("denied"));

            var result = await _service.GetFolderListByGraphId(_graphId, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("denied", result.Error);
        }

        [Fact]
        public async Task GetFolderList_ReturnsFolders()
        {
            var document = AuthorizeFullDocument();
            var folderId = Guid.NewGuid();
            document.Folders[folderId] = new FolderDefinition { Name = "Research" };

            var result = await _service.GetFolderListByGraphId(_graphId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("Research", result.Value!.Folders[folderId].Name);
        }

        [Fact]
        public async Task CreateFolder_EmptyName_Fails()
        {
            AuthorizeFullDocument();

            var result = await _service.CreateFolderByGraphId(
                new CreateFolderRequest { FolderName = "  ", FolderColor = "#fff" }, _graphId, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Failed to create: Folder name was empty.", result.Error);
        }

        [Fact]
        public async Task CreateFolder_AddsFolderAndSaves()
        {
            var document = AuthorizeFullDocument();

            var result = await _service.CreateFolderByGraphId(
                new CreateFolderRequest { FolderName = "Ideas", FolderColor = "#123" }, _graphId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("Ideas", result.Value!.FolderName);
            Assert.True(document.Folders.ContainsKey(result.Value.Id));
            Assert.Equal("#123", document.Folders[result.Value.Id].Color);
            _graphRepoMock.Verify(r => r.SaveAsync(document), Times.Once);
        }

        [Fact]
        public async Task EditFolder_UnknownFolder_Fails()
        {
            AuthorizeFullDocument();

            var result = await _service.EditFolderByIds(
                new EditFolderRequest { FolderName = "x", FolderColor = "#000" }, _graphId, Guid.NewGuid(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Folder not found.", result.Error);
        }

        [Fact]
        public async Task EditFolder_UpdatesNameAndColor()
        {
            var document = AuthorizeFullDocument();
            var folderId = Guid.NewGuid();
            document.Folders[folderId] = new FolderDefinition { Name = "old", Color = "#000" };

            var result = await _service.EditFolderByIds(
                new EditFolderRequest { FolderName = "new", FolderColor = "#fff" }, _graphId, folderId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("new", document.Folders[folderId].Name);
            Assert.Equal("#fff", document.Folders[folderId].Color);
        }

        [Fact]
        public async Task DeleteFolder_UnfilesMemberNodesWithoutDeletingThem()
        {
            var document = AuthorizeFullDocument();
            var folderId = Guid.NewGuid();
            document.Folders[folderId] = new FolderDefinition { Name = "Doomed" };
            var inFolder = TestData.NewNode("In Folder");
            inFolder.FolderId = folderId;
            var outside = TestData.NewNode("Outside");
            document.Nodes[inFolder.Id] = inFolder;
            document.Nodes[outside.Id] = outside;

            var result = await _service.DeleteFolderByIds(_graphId, folderId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("Doomed", result.Value!.FolderName);
            Assert.Empty(document.Folders);
            Assert.Null(inFolder.FolderId);
            Assert.Equal(2, document.Nodes.Count); // nodes themselves survive
            _nodeRepoMock.Verify(r => r.SaveManyAsync(_graphId,
                It.Is<IEnumerable<NoteNode>>(nodes => nodes.Single() == inFolder)), Times.Once);
        }

        [Fact]
        public async Task MoveNodeToFolder_UnknownFolder_Fails()
        {
            var document = AuthorizeFullDocument();
            var node = TestData.NewNode();
            document.Nodes[node.Id] = node;

            var result = await _service.MoveNodeToFolder(
                new MoveNodeToFolderRequest { FolderId = Guid.NewGuid() }, _graphId, node.Id, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Folder not found in graph.", result.Error);
        }

        [Fact]
        public async Task MoveNodeToFolder_NullFolderId_UnfilesNode()
        {
            var document = AuthorizeFullDocument();
            var node = TestData.NewNode();
            node.FolderId = Guid.NewGuid();
            document.Nodes[node.Id] = node;

            var result = await _service.MoveNodeToFolder(
                new MoveNodeToFolderRequest { FolderId = null }, _graphId, node.Id, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Null(node.FolderId);
            _nodeRepoMock.Verify(r => r.SaveAsync(_graphId, node), Times.Once);
        }

        [Fact]
        public async Task MoveNodesToFolder_SkipsMissingAndAlreadyFiledNodes()
        {
            var document = AuthorizeFullDocument();
            var folderId = Guid.NewGuid();
            document.Folders[folderId] = new FolderDefinition { Name = "Target" };
            var toMove = TestData.NewNode("Move Me");
            var alreadyThere = TestData.NewNode("Already There");
            alreadyThere.FolderId = folderId;
            var missingId = Guid.NewGuid();
            document.Nodes[toMove.Id] = toMove;
            document.Nodes[alreadyThere.Id] = alreadyThere;

            var result = await _service.MoveNodesToFolder(
                new MoveNodesToFolderRequest
                {
                    NodeIds = new List<Guid> { toMove.Id, alreadyThere.Id, missingId },
                    FolderId = folderId
                },
                _graphId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(new List<Guid> { toMove.Id }, result.Value!.UpdatedNodeIds);
            Assert.Contains(alreadyThere.Id, result.Value.SkippedNodeIds);
            Assert.Contains(missingId, result.Value.SkippedNodeIds);
            Assert.Equal(folderId, toMove.FolderId);
        }
    }
}
