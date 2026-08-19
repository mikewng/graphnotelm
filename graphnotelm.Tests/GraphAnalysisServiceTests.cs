using graphnotelm.Core.Models;
using graphnotelm.Core.Services;

namespace graphnotelm.Tests
{
    public class GraphAnalysisServiceTests
    {
        private readonly GraphAnalysisService _service = new();

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

        // The pathing methods are placeholders today; these tests pin down the
        // current contract (a successful Result) so implementing them for real
        // forces a deliberate test update.

        [Fact]
        public void FindLeastConfidentPath_ReturnsOkPlaceholder()
        {
            var result = _service.FindLeastConfidentPath(Guid.NewGuid());
            Assert.True(result.Success);
            Assert.NotNull(result.Value);
            Assert.NotEmpty(result.Value!);
        }

        [Fact]
        public void FindKnowledgeFrontier_ReturnsOkPlaceholder()
        {
            var result = _service.FindKnowledgeFrontier(Guid.NewGuid());
            Assert.True(result.Success);
            Assert.NotNull(result.Value);
        }

        [Fact]
        public void FindBestPathWithBudget_ReturnsOkPlaceholder()
        {
            var result = _service.FindBestPathWithBudget(Guid.NewGuid(), 1.0f);
            Assert.True(result.Success);
            Assert.NotNull(result.Value);
        }
    }
}
