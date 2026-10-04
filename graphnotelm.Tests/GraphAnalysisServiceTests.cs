using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Core.Utils;
using graphnotelm.Utils;
using Moq;

namespace graphnotelm.Tests
{
    public class GraphAnalysisServiceTests
    {
        private readonly Mock<INoteGraphAccessService> _accessMock = new();
        private readonly GraphAnalysisService _service;
        private readonly NoteGraphDocument _document = TestData.NewDocument(Guid.NewGuid());
        private readonly Guid _prerequisiteTo = Guid.NewGuid();
        private readonly FixedTimeProvider _time = new();

        public GraphAnalysisServiceTests()
        {
            _service = new GraphAnalysisService(_accessMock.Object, _time);
            _document.Relationships[_prerequisiteTo] = new RelationshipDefinition { Name = "prerequisite to", Inverse = "has prerequisite" };
            _accessMock.Setup(a => a.GetAuthorizedSkeletonDocumentAsync(_document.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphDocument>.Ok(_document));
        }

        private NoteNode AddNode(string title, float confidence = 0f)
        {
            var node = TestData.NewNode(title);
            node.Metadata.UserConfidenceRate = confidence;
            _document.Nodes[node.Id] = node;
            return node;
        }

        private void Link(NoteNode source, NoteNode target)
            => source.Relationships.Add(new NodeRelationship { TargetNodeId = target.Id, RelationshipId = _prerequisiteTo });

        [Fact]
        public void BuildView_ReturnsViewOverDocumentNodes()
        {
            var document = TestData.NewDocument(Guid.NewGuid());
            var source = TestData.NewNode("Source");
            var target = TestData.NewNode("Target");
            var relId = Guid.NewGuid();
            source.Relationships.Add(new NodeRelationship { TargetNodeId = target.Id, RelationshipId = relId });
            document.Nodes[source.Id] = source;
            document.Nodes[target.Id] = target;

            var view = _service.BuildView(document, source.Id);

            Assert.Same(source, view.GetNode(source.Id));
            Assert.Equal(2, view.AllNodes.Count);

            var outgoing = Assert.Single(view.GetOutgoing(source.Id));
            Assert.Equal(target.Id, outgoing.TargetNodeId);

            // Reverse adjacency stores the source node's ID in TargetNodeId
            var incoming = Assert.Single(view.GetIncoming(target.Id));
            Assert.Equal(source.Id, incoming.TargetNodeId);

            Assert.Equal(new List<Guid> { source.Id }, view.GetRootNodes());
            Assert.Equal(new List<Guid> { target.Id }, view.GetLeafNodes());
        }

        [Fact]
        public void BuildView_EmptyDocument_ProducesEmptyView()
        {
            var view = _service.BuildView(TestData.NewDocument(Guid.NewGuid()), Guid.NewGuid());

            Assert.Empty(view.AllNodes);
            Assert.Empty(view.GetRootNodes());
            Assert.Empty(view.GetLeafNodes());
        }

        [Fact]
        public async Task FindWeakestPath_ReturnsPathWithConfidence()
        {
            var start = AddNode("Start", 9);
            var target = AddNode("Target", 4);
            Link(start, target);

            var result = await _service.FindWeakestPath(_document.Id,
                new WeakestPathRequest { StartNodeId = start.Id, TargetNodeId = target.Id }, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(new[] { start.Id, target.Id }, result.Value!.Path.Select(n => n.Id));
            Assert.Equal("Target", result.Value.Path[1].Title);
            Assert.Equal(4f, result.Value.Path[1].Confidence);
        }

        [Fact]
        public async Task FindKnowledgeFrontier_UsesMeasuredConfidenceOverTheSelfRating()
        {
            var start = AddNode("Start", 8);
            // Rated 9 by the user, but forgotten at its last review two months ago.
            var forgotten = AddNode("Forgotten", 9);
            forgotten.Metadata.Memory = MemoryModel.Review(null, ReviewGrade.Again, _time.Now.AddDays(-60));
            Link(start, forgotten);

            var result = await _service.FindKnowledgeFrontier(_document.Id,
                new KnowledgeFrontierRequest { StartNodeId = start.Id, MinConfidence = 5f }, CancellationToken.None);

            var next = Assert.Single(result.Value!.Frontier);
            Assert.Equal(forgotten.Id, next.Id);
            Assert.InRange(next.Confidence, 0f, 2f);
        }

        [Fact]
        public async Task FindWeakestPath_UnknownTarget_Fails()
        {
            var start = AddNode("Start");

            var result = await _service.FindWeakestPath(_document.Id,
                new WeakestPathRequest { StartNodeId = start.Id, TargetNodeId = Guid.NewGuid() }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Target node not found in this graph.", result.Error);
        }

        [Fact]
        public async Task FindKnowledgeFrontier_ReturnsKnownAndFrontier()
        {
            var start = AddNode("Start", 8);
            var next = AddNode("Next", 1);
            Link(start, next);

            var result = await _service.FindKnowledgeFrontier(_document.Id,
                new KnowledgeFrontierRequest { StartNodeId = start.Id, MinConfidence = 5f }, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(start.Id, Assert.Single(result.Value!.Known).Id);
            Assert.Equal(next.Id, Assert.Single(result.Value.Frontier).Id);
        }

        [Fact]
        public async Task FindKnowledgeFrontier_UnknownRelationshipType_Fails()
        {
            var start = AddNode("Start");

            var result = await _service.FindKnowledgeFrontier(_document.Id,
                new KnowledgeFrontierRequest { StartNodeId = start.Id, RelationshipIds = { Guid.NewGuid() } }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Relationship type not found in this graph.", result.Error);
        }

        [Fact]
        public async Task FindLearningOrder_FiltersByRelationshipType()
        {
            var related = Guid.NewGuid();
            _document.Relationships[related] = new RelationshipDefinition { Name = "related to" };
            var basics = AddNode("Basics");
            var aside = AddNode("Aside");
            var target = AddNode("Target");
            Link(basics, target);
            aside.Relationships.Add(new NodeRelationship { TargetNodeId = target.Id, RelationshipId = related });

            var result = await _service.FindLearningOrder(_document.Id,
                new LearningOrderRequest { TargetNodeId = target.Id, RelationshipIds = { _prerequisiteTo } }, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(new[] { "Basics", "Target" }, result.Value!.Order.Select(n => n.Title));
            Assert.Empty(result.Value.Cyclic);
        }

        [Fact]
        public async Task FindLearningOrder_BothDirections_FailsInsteadOfThrowing()
        {
            var target = AddNode("Target");

            var result = await _service.FindLearningOrder(_document.Id,
                new LearningOrderRequest { TargetNodeId = target.Id, Direction = EdgeDirection.Both }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.StartsWith("A learning order needs a direction", result.Error);
        }

        [Fact]
        public async Task FindLearningOrder_UndefinedDirection_Fails()
        {
            var target = AddNode("Target");

            var result = await _service.FindLearningOrder(_document.Id,
                new LearningOrderRequest { TargetNodeId = target.Id, Direction = (EdgeDirection)42 }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Unknown edge direction '42'.", result.Error);
        }

        [Fact]
        public async Task Analysis_AccessDenied_ReturnsAccessError()
        {
            var otherGraphId = Guid.NewGuid();
            _accessMock.Setup(a => a.GetAuthorizedSkeletonDocumentAsync(otherGraphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphDocument>.Fail("UserId mismatch. Access to graph metadata denied."));

            var result = await _service.FindLearningOrder(otherGraphId,
                new LearningOrderRequest { TargetNodeId = Guid.NewGuid() }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("UserId mismatch. Access to graph metadata denied.", result.Error);
        }
    }
}
