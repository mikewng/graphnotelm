using graphnotelm.Core.Models;
using graphnotelm.Core.Utils;

namespace graphnotelm.Tests
{
    public class PathingAlgorithmsTests
    {
        private readonly NoteGraphDocument _document = TestData.NewDocument(Guid.NewGuid());
        private readonly Guid _prerequisiteTo = Guid.NewGuid();
        private readonly Guid _relatedTo = Guid.NewGuid();

        private NoteNode AddNode(string title, float confidence = 0f)
        {
            var node = TestData.NewNode(title);
            node.Metadata.UserConfidenceRate = confidence;
            _document.Nodes[node.Id] = node;
            return node;
        }

        private void Link(NoteNode source, NoteNode target, Guid? relationshipId = null)
            => source.Relationships.Add(new NodeRelationship
            {
                TargetNodeId = target.Id,
                RelationshipId = relationshipId ?? _prerequisiteTo
            });

        // GraphView builds its adjacency up front, so create it after the fixture is linked.
        private GraphView View() => new GraphView(_document);

        private static string Title(NoteNode node) => node.Title;

        // ── Weakest path (Dijkstra) ──────────────────────────────────────────

        [Fact]
        public void DijkstrasById_ReturnsPathWithLowestTotalConfidence()
        {
            var start = AddNode("Start", 9);
            var weak = AddNode("Weak", 1);
            var strong = AddNode("Strong", 8);
            var target = AddNode("Target", 5);
            Link(start, weak);
            Link(weak, target);
            Link(start, strong);
            Link(strong, target);

            var path = PathingAlgorithms.DijkstrasById(start.Id, target.Id, View(), Title);

            Assert.Equal(new[] { "Start", "Weak", "Target" }, path);
        }

        [Fact]
        public void DijkstrasById_EqualCost_PrefersFewerHops()
        {
            var start = AddNode("Start");
            var detour = AddNode("Detour", 0);
            var target = AddNode("Target", 5);
            Link(start, detour);
            Link(detour, target);
            Link(start, target);

            var path = PathingAlgorithms.DijkstrasById(start.Id, target.Id, View(), Title);

            Assert.Equal(new[] { "Start", "Target" }, path);
        }

        [Fact]
        public void DijkstrasById_StartIsTarget_ReturnsSingleNode()
        {
            var start = AddNode("Start");

            var path = PathingAlgorithms.DijkstrasById(start.Id, start.Id, View(), Title);

            Assert.Equal(new[] { "Start" }, path);
        }

        [Fact]
        public void DijkstrasById_Unreachable_ReturnsEmpty()
        {
            var start = AddNode("Start");
            var island = AddNode("Island");

            Assert.Empty(PathingAlgorithms.DijkstrasById(start.Id, island.Id, View(), Title));
        }

        [Fact]
        public void DijkstrasById_UnknownNode_ReturnsEmptyInsteadOfThrowing()
        {
            var start = AddNode("Start");

            Assert.Empty(PathingAlgorithms.DijkstrasById(start.Id, Guid.NewGuid(), View(), Title));
            Assert.Empty(PathingAlgorithms.DijkstrasById(Guid.NewGuid(), start.Id, View(), Title));
        }

        [Fact]
        public void DijkstrasById_Outgoing_DoesNotWalkEdgesBackwards()
        {
            var basics = AddNode("Basics");
            var advanced = AddNode("Advanced");
            Link(basics, advanced);
            var view = View();

            Assert.Empty(PathingAlgorithms.DijkstrasById(advanced.Id, basics.Id, view, Title, EdgeDirection.Outgoing));
            Assert.Equal(new[] { "Advanced", "Basics" },
                PathingAlgorithms.DijkstrasById(advanced.Id, basics.Id, view, Title, EdgeDirection.Both));
        }

        [Fact]
        public void DijkstrasById_RelationshipFilter_IgnoresOtherEdgeTypes()
        {
            var start = AddNode("Start");
            var step = AddNode("Step", 9);
            var target = AddNode("Target");
            Link(start, target, _relatedTo);
            Link(start, step);
            Link(step, target);

            var path = PathingAlgorithms.DijkstrasById(start.Id, target.Id, View(), Title,
                relationshipIds: new HashSet<Guid> { _prerequisiteTo });

            Assert.Equal(new[] { "Start", "Step", "Target" }, path);
        }

        // ── Knowledge frontier (BFS) ─────────────────────────────────────────

        [Fact]
        public void BreadthFirstSearchById_SplitsKnownNodesFromFrontier()
        {
            var start = AddNode("Start", 8);
            var known = AddNode("Known", 7);
            var gap = AddNode("Gap", 2);
            var beyondGap = AddNode("BeyondGap", 9);
            var weakNeighbor = AddNode("WeakNeighbor", 1);
            Link(start, known);
            Link(known, gap);
            Link(gap, beyondGap);
            Link(start, weakNeighbor);

            var result = PathingAlgorithms.BreadthFirstSearchById(start.Id, 5f, View(), Title);

            Assert.Equal(new[] { "Known", "Start" }, result.Known.Order());
            // The search stops at the frontier, so nodes past it aren't included.
            Assert.Equal(new[] { "Gap", "WeakNeighbor" }, result.Frontier.Order());
        }

        [Fact]
        public void BreadthFirstSearchById_StartBelowThreshold_IsTheFrontier()
        {
            var start = AddNode("Start", 1);
            var next = AddNode("Next", 9);
            Link(start, next);

            var result = PathingAlgorithms.BreadthFirstSearchById(start.Id, 5f, View(), Title);

            Assert.Empty(result.Known);
            Assert.Equal(new[] { "Start" }, result.Frontier);
        }

        [Fact]
        public void BreadthFirstSearchById_Outgoing_OnlyFollowsEdgesForward()
        {
            var earlier = AddNode("Earlier", 1);
            var start = AddNode("Start", 9);
            var later = AddNode("Later", 1);
            Link(earlier, start);
            Link(start, later);

            var result = PathingAlgorithms.BreadthFirstSearchById(start.Id, 5f, View(), Title, EdgeDirection.Outgoing);

            Assert.Equal(new[] { "Start" }, result.Known);
            Assert.Equal(new[] { "Later" }, result.Frontier);
        }

        [Fact]
        public void BreadthFirstSearchById_IgnoresEdgesToDeletedNodes()
        {
            var start = AddNode("Start", 9);
            start.Relationships.Add(new NodeRelationship { TargetNodeId = Guid.NewGuid(), RelationshipId = _prerequisiteTo });

            var result = PathingAlgorithms.BreadthFirstSearchById(start.Id, 5f, View(), Title);

            Assert.Equal(new[] { "Start" }, result.Known);
            Assert.Empty(result.Frontier);
        }

        [Fact]
        public void BreadthFirstSearchById_UnknownStart_ReturnsEmpty()
        {
            AddNode("Start", 9);

            var result = PathingAlgorithms.BreadthFirstSearchById(Guid.NewGuid(), 5f, View(), Title);

            Assert.Empty(result.Known);
            Assert.Empty(result.Frontier);
        }

        // ── Learning order (Kahn) ────────────────────────────────────────────

        [Fact]
        public void KahnTopologicalSortById_OrdersPrerequisitesBeforeTarget()
        {
            var a = AddNode("A");
            var b = AddNode("B");
            var c = AddNode("C");
            var d = AddNode("D");
            Link(a, b);
            Link(b, c);
            Link(d, c);

            var result = PathingAlgorithms.KahnTopologicalSortById(c.Id, View(), Title);

            // A and D are both ready first; ties go alphabetically.
            Assert.Equal(new[] { "A", "B", "D", "C" }, result.Order);
            Assert.Empty(result.Cyclic);
        }

        [Fact]
        public void KahnTopologicalSortById_SharedPrerequisite_AppearsOnce()
        {
            var basics = AddNode("Basics");
            var left = AddNode("Left");
            var right = AddNode("Right");
            var goal = AddNode("Goal");
            Link(basics, left);
            Link(basics, right);
            Link(left, goal);
            Link(right, goal);

            var result = PathingAlgorithms.KahnTopologicalSortById(goal.Id, View(), Title);

            Assert.Equal(new[] { "Basics", "Left", "Right", "Goal" }, result.Order);
        }

        [Fact]
        public void KahnTopologicalSortById_ExcludesNodesTheTargetDoesNotDependOn()
        {
            var prerequisite = AddNode("Prerequisite");
            var target = AddNode("Target");
            var dependent = AddNode("Dependent");
            AddNode("Unconnected");
            Link(prerequisite, target);
            Link(target, dependent);

            var result = PathingAlgorithms.KahnTopologicalSortById(target.Id, View(), Title);

            Assert.Equal(new[] { "Prerequisite", "Target" }, result.Order);
        }

        [Fact]
        public void KahnTopologicalSortById_RelationshipFilter_IgnoresOtherEdgeTypes()
        {
            var prerequisite = AddNode("Prerequisite");
            var related = AddNode("Related");
            var target = AddNode("Target");
            Link(prerequisite, target);
            Link(related, target, _relatedTo);
            var view = View();

            var filtered = PathingAlgorithms.KahnTopologicalSortById(target.Id, view, Title,
                new HashSet<Guid> { _prerequisiteTo });
            var unfiltered = PathingAlgorithms.KahnTopologicalSortById(target.Id, view, Title);

            Assert.Equal(new[] { "Prerequisite", "Target" }, filtered.Order);
            Assert.Equal(new[] { "Prerequisite", "Related", "Target" }, unfiltered.Order);
        }

        [Fact]
        public void KahnTopologicalSortById_Incoming_ReadsEdgesAsDependsOn()
        {
            var a = AddNode("A");
            var b = AddNode("B");
            var c = AddNode("C");
            // C depends on B, which depends on A.
            Link(c, b);
            Link(b, a);

            var result = PathingAlgorithms.KahnTopologicalSortById(c.Id, View(), Title,
                direction: EdgeDirection.Incoming);

            Assert.Equal(new[] { "A", "B", "C" }, result.Order);
        }

        [Fact]
        public void KahnTopologicalSortById_Cycle_ReportsNodesThatCannotBeOrdered()
        {
            var a = AddNode("A");
            var b = AddNode("B");
            var target = AddNode("Target");
            var free = AddNode("Free");
            Link(a, b);
            Link(b, a);
            Link(b, target);
            Link(free, target);

            var result = PathingAlgorithms.KahnTopologicalSortById(target.Id, View(), Title);

            Assert.Equal(new[] { "Free" }, result.Order);
            // The target waits on B, so it is stuck behind the cycle too.
            Assert.Equal(new[] { "A", "B", "Target" }, result.Cyclic);
        }

        [Fact]
        public void KahnTopologicalSortById_NoPrerequisites_ReturnsTargetAlone()
        {
            var target = AddNode("Target");

            var result = PathingAlgorithms.KahnTopologicalSortById(target.Id, View(), Title);

            Assert.Equal(new[] { "Target" }, result.Order);
            Assert.Empty(result.Cyclic);
        }

        [Fact]
        public void KahnTopologicalSortById_UnknownTarget_ReturnsEmpty()
        {
            AddNode("Target");

            var result = PathingAlgorithms.KahnTopologicalSortById(Guid.NewGuid(), View(), Title);

            Assert.Empty(result.Order);
            Assert.Empty(result.Cyclic);
        }

        [Fact]
        public void KahnTopologicalSortById_BothDirections_Throws()
        {
            var target = AddNode("Target");

            Assert.Throws<ArgumentException>(() =>
                PathingAlgorithms.KahnTopologicalSortById(target.Id, View(), Title, direction: EdgeDirection.Both));
        }
    }
}
