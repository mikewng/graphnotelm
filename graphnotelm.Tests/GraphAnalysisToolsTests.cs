using graphnotelm.Core.Models;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Core.Utils.Tools;
using Moq;

namespace graphnotelm.Tests
{
    public class GraphAnalysisToolsTests
    {
        private readonly NoteGraphDocument _document = TestData.NewDocument(Guid.NewGuid());
        private readonly Guid _prerequisiteTo = Guid.NewGuid();
        private readonly Guid _relatedTo = Guid.NewGuid();

        public GraphAnalysisToolsTests()
        {
            _document.Relationships[_prerequisiteTo] = new RelationshipDefinition { Name = "prerequisite to", Inverse = "has prerequisite" };
            _document.Relationships[_relatedTo] = new RelationshipDefinition { Name = "related to", Inverse = "related to" };
        }

        private NoteNode AddNode(string title)
        {
            var node = TestData.NewNode(title);
            _document.Nodes[node.Id] = node;
            return node;
        }

        private static void Link(NoteNode source, NoteNode target, Guid relationshipId)
            => source.Relationships.Add(new NodeRelationship { TargetNodeId = target.Id, RelationshipId = relationshipId });

        // The tools build their GraphView on construction, so create them after the fixture is linked.
        private GraphAnalysisTools Tools() => new GraphAnalysisTools(_document, new Mock<IGraphAnalysisService>().Object);

        [Fact]
        public void FindLearningOrder_ByRelationshipName_FollowsOnlyThatType()
        {
            var basics = AddNode("Basics");
            var aside = AddNode("Aside");
            var target = AddNode("Target");
            Link(basics, target, _prerequisiteTo);
            Link(aside, target, _relatedTo);

            var result = Tools().FindLearningOrder(target.Id, "Prerequisite To");

            Assert.Null(result.Error);
            Assert.Equal(new[] { "Basics", "Target" }, result.Order.Select(n => n.Title));
        }

        [Fact]
        public void FindLearningOrder_ByInverseName_ReadsEdgesBackwards()
        {
            var basics = AddNode("Basics");
            var target = AddNode("Target");
            Link(basics, target, _prerequisiteTo);

            // "Target has prerequisite Basics" points from a node to its prerequisite.
            var result = Tools().FindLearningOrder(target.Id, "has prerequisite", pointsToPrerequisite: true);

            Assert.Null(result.Error);
            Assert.Equal(new[] { "Basics", "Target" }, result.Order.Select(n => n.Title));
        }

        [Fact]
        public void FindLearningOrder_UnknownRelationship_ListsAvailableTypes()
        {
            var target = AddNode("Target");

            var result = Tools().FindLearningOrder(target.Id, "requires");

            Assert.Empty(result.Order);
            Assert.Equal("No relationship type named 'requires'. Available: prerequisite to, related to.", result.Error);
        }

        [Fact]
        public void FindLearningOrder_UnknownNode_ReturnsErrorInsteadOfThrowing()
        {
            AddNode("Target");

            var result = Tools().FindLearningOrder(Guid.NewGuid());

            Assert.Empty(result.Order);
            Assert.Equal("No node with that ID exists in this graph.", result.Error);
        }
    }
}
