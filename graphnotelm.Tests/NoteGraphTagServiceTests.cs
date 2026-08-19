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
    public class NoteGraphTagServiceTests
    {
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _graphId = Guid.NewGuid();

        private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
        private readonly Mock<INoteGraphAccessService> _accessMock = new();
        private readonly Mock<INoteGraphRepository> _graphRepoMock = new();
        private readonly Mock<INoteNodeRepository> _nodeRepoMock = new();

        private readonly NoteGraphTagService _service;

        public NoteGraphTagServiceTests()
        {
            _service = new NoteGraphTagService(
                _unitOfWorkMock.Object,
                _accessMock.Object,
                _graphRepoMock.Object,
                _nodeRepoMock.Object);
        }

        private NoteGraphDocument AuthorizeFullDocument()
        {
            var document = TestData.NewDocument(_userId, _graphId);
            _accessMock.Setup(a => a.GetAuthorizedFullDocumentAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphDocument>.Ok(document));
            return document;
        }

        [Fact]
        public async Task GetTagList_AccessDenied_Fails()
        {
            _accessMock.Setup(a => a.GetAuthorizedFullDocumentAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphDocument>.Fail("denied"));

            var result = await _service.GetTagListByGraphId(_graphId, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("denied", result.Error);
        }

        [Fact]
        public async Task GetTagList_ReturnsTagNames()
        {
            var document = AuthorizeFullDocument();
            document.Tags[Guid.NewGuid()] = new TagDefinition { Name = "alpha" };
            document.Tags[Guid.NewGuid()] = new TagDefinition { Name = "beta" };

            var result = await _service.GetTagListByGraphId(_graphId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(new[] { "alpha", "beta" }, result.Value!.Tags.OrderBy(t => t));
        }

        [Fact]
        public async Task CreateTag_AddsTagAndSavesDocument()
        {
            var document = AuthorizeFullDocument();

            var result = await _service.CreateTagByGraphId(
                new CreateTagRequest { TagName = "new-tag", TagColor = "#fff" }, _graphId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("new-tag", result.Value!.TagName);
            var tag = Assert.Single(document.Tags).Value;
            Assert.Equal("new-tag", tag.Name);
            Assert.Equal("#fff", tag.Color);
            _graphRepoMock.Verify(r => r.SaveAsync(document), Times.Once);
        }

        [Fact]
        public async Task EditTag_UnknownTag_Fails()
        {
            AuthorizeFullDocument();

            var result = await _service.EditTagByIds(
                new EditTagRequest { TagName = "x", TagColor = "#000" }, _graphId, Guid.NewGuid(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Tag not found.", result.Error);
        }

        [Fact]
        public async Task EditTag_UpdatesNameAndColor()
        {
            var document = AuthorizeFullDocument();
            var tagId = Guid.NewGuid();
            document.Tags[tagId] = new TagDefinition { Name = "old", Color = "#000" };

            var result = await _service.EditTagByIds(
                new EditTagRequest { TagName = "new", TagColor = "#fff" }, _graphId, tagId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("new", document.Tags[tagId].Name);
            Assert.Equal("#fff", document.Tags[tagId].Color);
            _graphRepoMock.Verify(r => r.SaveAsync(document), Times.Once);
        }

        [Fact]
        public async Task DeleteTag_RemovesTagFromGraphAndAllNodes()
        {
            var document = AuthorizeFullDocument();
            var tagId = Guid.NewGuid();
            document.Tags[tagId] = new TagDefinition { Name = "doomed" };
            var tagged = TestData.NewNode("Tagged");
            tagged.Tags.Add(tagId);
            var untagged = TestData.NewNode("Untagged");
            document.Nodes[tagged.Id] = tagged;
            document.Nodes[untagged.Id] = untagged;

            var result = await _service.DeleteTagByIds(_graphId, tagId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("doomed", result.Value!.TagName);
            Assert.Empty(document.Tags);
            Assert.Empty(tagged.Tags);
            _nodeRepoMock.Verify(r => r.SaveAsync(_graphId, tagged), Times.Once);
            _nodeRepoMock.Verify(r => r.SaveAsync(_graphId, untagged), Times.Never);
        }

        [Fact]
        public async Task AddTagToNode_UnknownTag_Fails()
        {
            var document = AuthorizeFullDocument();
            var node = TestData.NewNode();
            document.Nodes[node.Id] = node;

            var result = await _service.AddTagToNode(
                new AddNodeTagRequest { TagId = Guid.NewGuid() }, _graphId, node.Id, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Tag not found in graph.", result.Error);
        }

        [Fact]
        public async Task AddTagToNode_AlreadyAssigned_Fails()
        {
            var document = AuthorizeFullDocument();
            var tagId = Guid.NewGuid();
            document.Tags[tagId] = new TagDefinition { Name = "tag" };
            var node = TestData.NewNode();
            node.Tags.Add(tagId);
            document.Nodes[node.Id] = node;

            var result = await _service.AddTagToNode(
                new AddNodeTagRequest { TagId = tagId }, _graphId, node.Id, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Tag is already assigned to this node.", result.Error);
        }

        [Fact]
        public async Task AddTagToNode_Valid_AssignsAndSaves()
        {
            var document = AuthorizeFullDocument();
            var tagId = Guid.NewGuid();
            document.Tags[tagId] = new TagDefinition { Name = "tag" };
            var node = TestData.NewNode();
            document.Nodes[node.Id] = node;

            var result = await _service.AddTagToNode(
                new AddNodeTagRequest { TagId = tagId }, _graphId, node.Id, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains(tagId, node.Tags);
            _nodeRepoMock.Verify(r => r.SaveAsync(_graphId, node), Times.Once);
        }

        [Fact]
        public async Task AddTagToManyNodes_TracksAppliedAndSkipped()
        {
            var document = AuthorizeFullDocument();
            var tagId = Guid.NewGuid();
            var unknownTagId = Guid.NewGuid();
            document.Tags[tagId] = new TagDefinition { Name = "tag" };
            var node = TestData.NewNode();
            var missingNodeId = Guid.NewGuid();
            document.Nodes[node.Id] = node;

            var result = await _service.AddTagToManyNodes(
                new AddTagToNodesRequest
                {
                    NodeIds = new List<Guid> { node.Id, missingNodeId },
                    TagIds = new List<Guid> { tagId, unknownTagId }
                },
                _graphId, CancellationToken.None);

            Assert.True(result.Success);
            var applied = Assert.Single(result.Value!.Applied);
            Assert.Equal(node.Id, applied.NodeId);
            Assert.Equal(tagId, applied.TagId);
            // Skipped: unknown tag on the real node + both tags on the missing node
            Assert.Equal(3, result.Value.Skipped.Count);
            _nodeRepoMock.Verify(r => r.SaveManyAsync(_graphId,
                It.Is<IEnumerable<NoteNode>>(nodes => nodes.Single() == node)), Times.Once);
        }

        [Fact]
        public async Task RemoveTagFromManyNodes_TracksAppliedAndSkipped()
        {
            var document = AuthorizeFullDocument();
            var tagId = Guid.NewGuid();
            var hasTag = TestData.NewNode("Has");
            hasTag.Tags.Add(tagId);
            var lacksTag = TestData.NewNode("Lacks");
            document.Nodes[hasTag.Id] = hasTag;
            document.Nodes[lacksTag.Id] = lacksTag;

            var result = await _service.RemoveTagFromManyNodes(
                new RemoveTagFromNodesRequest
                {
                    NodeIds = new List<Guid> { hasTag.Id, lacksTag.Id },
                    TagIds = new List<Guid> { tagId }
                },
                _graphId, CancellationToken.None);

            Assert.True(result.Success);
            var applied = Assert.Single(result.Value!.Applied);
            Assert.Equal(hasTag.Id, applied.NodeId);
            var skipped = Assert.Single(result.Value.Skipped);
            Assert.Equal(lacksTag.Id, skipped.NodeId);
            Assert.Empty(hasTag.Tags);
        }

        [Fact]
        public async Task RemoveTagFromNode_TagNotAssigned_Fails()
        {
            var document = AuthorizeFullDocument();
            var node = TestData.NewNode();
            document.Nodes[node.Id] = node;

            var result = await _service.RemoveTagFromNode(_graphId, node.Id, Guid.NewGuid(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Tag is not assigned to this node.", result.Error);
        }

        [Fact]
        public async Task RemoveTagFromNode_Valid_RemovesAndSaves()
        {
            var document = AuthorizeFullDocument();
            var tagId = Guid.NewGuid();
            var node = TestData.NewNode();
            node.Tags.Add(tagId);
            document.Nodes[node.Id] = node;

            var result = await _service.RemoveTagFromNode(_graphId, node.Id, tagId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(tagId, result.Value!.RemovedTagId);
            Assert.Empty(node.Tags);
            _nodeRepoMock.Verify(r => r.SaveAsync(_graphId, node), Times.Once);
        }
    }
}
