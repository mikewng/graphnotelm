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
    public class NoteGraphRelationshipServiceTests
    {
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _graphId = Guid.NewGuid();

        private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
        private readonly Mock<INoteGraphAccessService> _accessMock = new();
        private readonly Mock<INoteGraphRepository> _graphRepoMock = new();
        private readonly Mock<INoteNodeRepository> _nodeRepoMock = new();

        private readonly NoteGraphRelationshipService _service;

        public NoteGraphRelationshipServiceTests()
        {
            _service = new NoteGraphRelationshipService(
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
        public async Task GetRelationshipList_AccessDenied_Fails()
        {
            _accessMock.Setup(a => a.GetAuthorizedFullDocumentAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphDocument>.Fail("denied"));

            var result = await _service.GetRelationshipListByGraphId(_graphId, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("denied", result.Error);
        }

        [Fact]
        public async Task GetRelationshipList_ReturnsRelationshipNames()
        {
            var document = AuthorizeFullDocument();
            document.Relationships[Guid.NewGuid()] = new RelationshipDefinition { Name = "depends on" };
            document.Relationships[Guid.NewGuid()] = new RelationshipDefinition { Name = "causes" };

            var result = await _service.GetRelationshipListByGraphId(_graphId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(new[] { "causes", "depends on" }, result.Value!.Relationships.OrderBy(r => r));
        }

        [Fact]
        public async Task CreateRelationship_AddsDefinitionAndSaves()
        {
            var document = AuthorizeFullDocument();

            var result = await _service.CreateRelationshipByGraphId(
                new CreateRelationshipRequest { Type = "supports", Color = "#0f0", Inverse = "supported by" },
                _graphId, CancellationToken.None);

            Assert.True(result.Success);
            var definition = Assert.Single(document.Relationships).Value;
            Assert.Equal("supports", definition.Name);
            Assert.Equal("#0f0", definition.Color);
            Assert.Equal("supported by", definition.Inverse);
            _graphRepoMock.Verify(r => r.SaveAsync(document), Times.Once);
        }

        [Fact]
        public async Task EditRelationship_UnknownId_Fails()
        {
            AuthorizeFullDocument();

            var result = await _service.EditRelationshipByIds(
                new EditRelationshipRequest { Type = "x", Color = "#000", Inverse = "y" },
                _graphId, Guid.NewGuid(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Relationship not found.", result.Error);
        }

        [Fact]
        public async Task EditRelationship_UpdatesDefinition()
        {
            var document = AuthorizeFullDocument();
            var relId = Guid.NewGuid();
            document.Relationships[relId] = new RelationshipDefinition { Name = "old", Color = "#000", Inverse = "old-inv" };

            var result = await _service.EditRelationshipByIds(
                new EditRelationshipRequest { Type = "new", Color = "#fff", Inverse = "new-inv" },
                _graphId, relId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("new", document.Relationships[relId].Name);
            Assert.Equal("#fff", document.Relationships[relId].Color);
            Assert.Equal("new-inv", document.Relationships[relId].Inverse);
        }

        [Fact]
        public async Task DeleteRelationship_RemovesDefinitionAndNodeEdges()
        {
            var document = AuthorizeFullDocument();
            var relId = Guid.NewGuid();
            document.Relationships[relId] = new RelationshipDefinition { Name = "doomed" };
            var usesRel = TestData.NewNode("Uses");
            usesRel.Relationships.Add(new NodeRelationship { TargetNodeId = Guid.NewGuid(), RelationshipId = relId });
            var otherRel = TestData.NewNode("Other");
            otherRel.Relationships.Add(new NodeRelationship { TargetNodeId = Guid.NewGuid(), RelationshipId = Guid.NewGuid() });
            document.Nodes[usesRel.Id] = usesRel;
            document.Nodes[otherRel.Id] = otherRel;

            var result = await _service.DeleteRelationshipByIds(_graphId, relId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Empty(document.Relationships);
            Assert.Empty(usesRel.Relationships);
            Assert.Single(otherRel.Relationships);
            _nodeRepoMock.Verify(r => r.SaveAsync(_graphId, usesRel), Times.Once);
            _nodeRepoMock.Verify(r => r.SaveAsync(_graphId, otherRel), Times.Never);
        }

        [Fact]
        public async Task DeleteRelationship_UnknownId_Fails()
        {
            AuthorizeFullDocument();

            var result = await _service.DeleteRelationshipByIds(_graphId, Guid.NewGuid(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Relationship not found.", result.Error);
        }

        // ---------- AddRelationshipToNode ----------

        [Fact]
        public async Task AddRelationshipToNode_SelfReference_Fails()
        {
            var document = AuthorizeFullDocument();
            var node = TestData.NewNode();
            document.Nodes[node.Id] = node;

            var result = await _service.AddRelationshipToNode(
                new AddNodeRelationshipRequest { TargetNodeId = node.Id, RelationshipId = Guid.NewGuid() },
                _graphId, node.Id, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("A node cannot have a relationship with itself.", result.Error);
        }

        [Fact]
        public async Task AddRelationshipToNode_UnknownTarget_Fails()
        {
            var document = AuthorizeFullDocument();
            var node = TestData.NewNode();
            document.Nodes[node.Id] = node;

            var result = await _service.AddRelationshipToNode(
                new AddNodeRelationshipRequest { TargetNodeId = Guid.NewGuid(), RelationshipId = Guid.NewGuid() },
                _graphId, node.Id, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Target node not found in graph.", result.Error);
        }

        [Fact]
        public async Task AddRelationshipToNode_UnknownRelationshipType_Fails()
        {
            var document = AuthorizeFullDocument();
            var node = TestData.NewNode();
            var target = TestData.NewNode();
            document.Nodes[node.Id] = node;
            document.Nodes[target.Id] = target;

            var result = await _service.AddRelationshipToNode(
                new AddNodeRelationshipRequest { TargetNodeId = target.Id, RelationshipId = Guid.NewGuid() },
                _graphId, node.Id, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Relationship type not found in graph.", result.Error);
        }

        [Fact]
        public async Task AddRelationshipToNode_Duplicate_Fails()
        {
            var document = AuthorizeFullDocument();
            var relId = Guid.NewGuid();
            document.Relationships[relId] = new RelationshipDefinition { Name = "rel" };
            var node = TestData.NewNode();
            var target = TestData.NewNode();
            node.Relationships.Add(new NodeRelationship { TargetNodeId = target.Id, RelationshipId = relId });
            document.Nodes[node.Id] = node;
            document.Nodes[target.Id] = target;

            var result = await _service.AddRelationshipToNode(
                new AddNodeRelationshipRequest { TargetNodeId = target.Id, RelationshipId = relId },
                _graphId, node.Id, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("This relationship already exists on the node.", result.Error);
        }

        [Fact]
        public async Task AddRelationshipToNode_Valid_AddsEdgeAndSaves()
        {
            var document = AuthorizeFullDocument();
            var relId = Guid.NewGuid();
            document.Relationships[relId] = new RelationshipDefinition { Name = "rel" };
            var node = TestData.NewNode();
            var target = TestData.NewNode();
            document.Nodes[node.Id] = node;
            document.Nodes[target.Id] = target;

            var result = await _service.AddRelationshipToNode(
                new AddNodeRelationshipRequest { TargetNodeId = target.Id, RelationshipId = relId },
                _graphId, node.Id, CancellationToken.None);

            Assert.True(result.Success);
            var edge = Assert.Single(node.Relationships);
            Assert.Equal(target.Id, edge.TargetNodeId);
            Assert.Equal(relId, edge.RelationshipId);
            _nodeRepoMock.Verify(r => r.SaveAsync(_graphId, node), Times.Once);
        }

        // ---------- RemoveRelationshipFromNode ----------

        [Fact]
        public async Task RemoveRelationshipFromNode_NotOnNode_Fails()
        {
            var document = AuthorizeFullDocument();
            var node = TestData.NewNode();
            document.Nodes[node.Id] = node;

            var result = await _service.RemoveRelationshipFromNode(
                _graphId, node.Id, Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Relationship not found on node.", result.Error);
        }

        [Fact]
        public async Task RemoveRelationshipFromNode_Valid_RemovesEdge()
        {
            var document = AuthorizeFullDocument();
            var relId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var node = TestData.NewNode();
            node.Relationships.Add(new NodeRelationship { TargetNodeId = targetId, RelationshipId = relId });
            document.Nodes[node.Id] = node;

            var result = await _service.RemoveRelationshipFromNode(
                _graphId, node.Id, targetId, relId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(targetId, result.Value!.RemovedTargetNodeId);
            Assert.Empty(node.Relationships);
            _nodeRepoMock.Verify(r => r.SaveAsync(_graphId, node), Times.Once);
        }
    }
}
