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
            Assert.Empty(result.Cycles);
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
        public void KahnTopologicalSortById_Cycle_OrdersTheCycleAsOneGroup()
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

            // The target waits on the cycle, but no longer gets stuck behind it.
            Assert.Equal(new[] { "A", "B", "Free", "Target" }, result.Order);
            Assert.Equal(new[] { "A", "B" }, Assert.Single(result.Cycles));
        }

        [Fact]
        public void KahnTopologicalSortById_Cycle_WaitsForPrerequisitesOutsideIt()
        {
            var zed = AddNode("Zed");
            var a = AddNode("A");
            var b = AddNode("B");
            var target = AddNode("Target");
            Link(zed, b);
            Link(a, b);
            Link(b, a);
            Link(a, target);

            var result = PathingAlgorithms.KahnTopologicalSortById(target.Id, View(), Title);

            // Only B needs Zed, but A can't be learned without B, so the whole cycle waits.
            Assert.Equal(new[] { "Zed", "A", "B", "Target" }, result.Order);
        }

        [Fact]
        public void KahnTopologicalSortById_SelfPrerequisite_IsReportedAsACycle()
        {
            var loop = AddNode("Loop");
            var target = AddNode("Target");
            Link(loop, loop);
            Link(loop, target);

            var result = PathingAlgorithms.KahnTopologicalSortById(target.Id, View(), Title);

            Assert.Equal(new[] { "Loop", "Target" }, result.Order);
            Assert.Equal(new[] { "Loop" }, Assert.Single(result.Cycles));
        }

        [Fact]
        public void KahnTopologicalSortById_NoPrerequisites_ReturnsTargetAlone()
        {
            var target = AddNode("Target");

            var result = PathingAlgorithms.KahnTopologicalSortById(target.Id, View(), Title);

            Assert.Equal(new[] { "Target" }, result.Order);
            Assert.Empty(result.Cycles);
        }

        [Fact]
        public void KahnTopologicalSortById_UnknownTarget_ReturnsEmpty()
        {
            AddNode("Target");

            var result = PathingAlgorithms.KahnTopologicalSortById(Guid.NewGuid(), View(), Title);

            Assert.Empty(result.Order);
            Assert.Empty(result.Cycles);
        }

        [Fact]
        public void KahnTopologicalSortById_BothDirections_Throws()
        {
            var target = AddNode("Target");

            Assert.Throws<ArgumentException>(() =>
                PathingAlgorithms.KahnTopologicalSortById(target.Id, View(), Title, direction: EdgeDirection.Both));
        }

        // ── Ready to learn (outer fringe) ────────────────────────────────────

        [Fact]
        public void FindReadyToLearn_NeedsEveryPrerequisiteKnown()
        {
            var basics = AddNode("Basics", 8);
            var algebra = AddNode("Algebra", 1);
            var calculus = AddNode("Calculus", 1);
            Link(basics, algebra);
            Link(basics, calculus);
            Link(algebra, calculus);

            var ready = PathingAlgorithms.FindReadyToLearn(5f, View(), Title);

            // Calculus sits next to the known Basics, but still waits on Algebra.
            Assert.Equal(new[] { "Algebra" }, ready);
        }

        [Fact]
        public void FindReadyToLearn_UnknownNodeWithoutPrerequisites_IsReady()
        {
            var foundations = AddNode("Foundations", 1);
            var next = AddNode("Next", 1);
            Link(foundations, next);

            Assert.Equal(new[] { "Foundations" }, PathingAlgorithms.FindReadyToLearn(5f, View(), Title));
        }

        [Fact]
        public void FindReadyToLearn_Cycle_IsReadyOnceOutsidePrerequisitesAreKnown()
        {
            var outside = AddNode("Outside", 1);
            var a = AddNode("A", 1);
            var b = AddNode("B", 1);
            Link(outside, a);
            Link(a, b);
            Link(b, a);
            var view = View();

            Assert.Equal(new[] { "Outside" }, PathingAlgorithms.FindReadyToLearn(5f, view, Title));

            // B's only prerequisite is A, but the two are learned together, so B waits on Outside too.
            outside.Metadata.UserConfidenceRate = 8;
            Assert.Equal(new[] { "A", "B" }, PathingAlgorithms.FindReadyToLearn(5f, view, Title));
        }

        [Fact]
        public void FindReadyToLearn_LeavesOutNodesOffEveryPrerequisiteChain()
        {
            var known = AddNode("Known", 8);
            var next = AddNode("Next", 1);
            var aside = AddNode("Aside", 1);
            AddNode("Island", 1);
            Link(known, next);
            Link(aside, next, _relatedTo);

            var ready = PathingAlgorithms.FindReadyToLearn(5f, View(), Title,
                new HashSet<Guid> { _prerequisiteTo });

            // Aside's only edge isn't a prerequisite link, so it neither blocks Next nor shows up.
            Assert.Equal(new[] { "Next" }, ready);
        }

        [Fact]
        public void FindReadyToLearn_Incoming_ReadsEdgesAsDependsOn()
        {
            var basics = AddNode("Basics", 8);
            var advanced = AddNode("Advanced", 1);
            // Advanced depends on Basics.
            Link(advanced, basics);

            Assert.Equal(new[] { "Advanced" },
                PathingAlgorithms.FindReadyToLearn(5f, View(), Title, direction: EdgeDirection.Incoming));
        }

        [Fact]
        public void FindReadyToLearn_EverythingKnown_ReturnsEmpty()
        {
            var a = AddNode("A", 8);
            var b = AddNode("B", 9);
            Link(a, b);

            Assert.Empty(PathingAlgorithms.FindReadyToLearn(5f, View(), Title));
        }

        [Fact]
        public void FindReadyToLearn_BothDirections_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                PathingAlgorithms.FindReadyToLearn(5f, View(), Title, direction: EdgeDirection.Both));
        }

        // ── Bottlenecks ──────────────────────────────────────────────────────

        private static (string Title, int Dependents) Bottleneck(NoteNode node, int dependents) => (node.Title, dependents);

        [Fact]
        public void FindBottlenecks_RanksByDependentsTimesWeakness()
        {
            var foundation = AddNode("Foundation", 6);
            var weak = AddNode("Weak", 1);
            var lone = AddNode("Lone", 0);
            var steps = Enumerable.Range(1, 5).Select(i => AddNode($"Step{i}", 10)).ToList();
            Link(foundation, steps[0]);
            for (int i = 1; i < steps.Count; i++)
                Link(steps[i - 1], steps[i]);
            Link(weak, steps[4]);
            var view = View();

            var bottlenecks = PathingAlgorithms.FindBottlenecks(view, Bottleneck, 10);

            // Foundation: 5 dependents × 4 = 20 beats Weak: 1 × 9 = 9. Fully known steps and Lone,
            // which nothing depends on, aren't bottlenecks.
            Assert.Equal(new[] { ("Foundation", 5), ("Weak", 1) }, bottlenecks);
        }

        [Fact]
        public void FindBottlenecks_SharedDependent_CountsOnce()
        {
            var basics = AddNode("Basics");
            var left = AddNode("Left", 10);
            var right = AddNode("Right", 10);
            var goal = AddNode("Goal", 10);
            Link(basics, left);
            Link(basics, right);
            Link(left, goal);
            Link(right, goal);

            var bottleneck = Assert.Single(PathingAlgorithms.FindBottlenecks(View(), Bottleneck, 10));

            Assert.Equal(("Basics", 3), bottleneck);
        }

        [Fact]
        public void FindBottlenecks_Cycle_DoesNotCountTheNodeAsItsOwnDependent()
        {
            var a = AddNode("A");
            var b = AddNode("B");
            Link(a, b);
            Link(b, a);

            Assert.Equal(new[] { ("A", 1), ("B", 1) }, PathingAlgorithms.FindBottlenecks(View(), Bottleneck, 10));
        }

        [Fact]
        public void FindBottlenecks_Limit_KeepsTheTopResults()
        {
            var top = AddNode("Top");
            var middle = AddNode("Middle");
            var bottom = AddNode("Bottom");
            Link(top, middle);
            Link(middle, bottom);

            Assert.Equal(new[] { ("Top", 2) }, PathingAlgorithms.FindBottlenecks(View(), Bottleneck, 1));
        }

        [Fact]
        public void FindBottlenecks_Incoming_CountsNodesThatDependOnIt()
        {
            var basics = AddNode("Basics");
            var advanced = AddNode("Advanced", 10);
            // Advanced depends on Basics.
            Link(advanced, basics);

            Assert.Equal(new[] { ("Basics", 1) },
                PathingAlgorithms.FindBottlenecks(View(), Bottleneck, 10, direction: EdgeDirection.Incoming));
        }

        [Fact]
        public void FindBottlenecks_RelationshipFilter_IgnoresOtherEdgeTypes()
        {
            var basics = AddNode("Basics");
            var aside = AddNode("Aside", 10);
            Link(basics, aside, _relatedTo);

            Assert.Empty(PathingAlgorithms.FindBottlenecks(View(), Bottleneck, 10,
                new HashSet<Guid> { _prerequisiteTo }));
        }

        [Fact]
        public void FindBottlenecks_BothDirections_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                PathingAlgorithms.FindBottlenecks(View(), Bottleneck, 10, direction: EdgeDirection.Both));
        }

        // ── Strongly connected components (Tarjan) ───────────────────────────

        [Fact]
        public void StronglyConnectedComponents_GroupsEachCycle()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();
            var d = Guid.NewGuid();
            var e = Guid.NewGuid();
            // a ⇄ b → c → d → e → c
            var next = new Dictionary<Guid, List<Guid>>
            {
                [a] = new() { b },
                [b] = new() { a, c },
                [c] = new() { d },
                [d] = new() { e },
                [e] = new() { c },
            };

            var components = PathingAlgorithms.StronglyConnectedComponents(next.Keys, id => next[id]);

            Assert.Equal(2, components.Count);
            // A group comes after every group it reaches.
            Assert.Equal(new[] { c, d, e }.Order(), components[0].Order());
            Assert.Equal(new[] { a, b }.Order(), components[1].Order());
        }

        [Fact]
        public void StronglyConnectedComponents_NodeOnNoCycle_IsAGroupOfItsOwn()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var next = new Dictionary<Guid, List<Guid>> { [a] = new() { b }, [b] = new() };

            var components = PathingAlgorithms.StronglyConnectedComponents(next.Keys, id => next[id]);

            Assert.Equal(new[] { new[] { b }, new[] { a } }, components.Select(g => g.ToArray()));
        }

        [Fact]
        public void StronglyConnectedComponents_LongChain_DoesNotOverflowTheStack()
        {
            var chain = Enumerable.Range(0, 200_000).Select(_ => Guid.NewGuid()).ToList();
            var next = new Dictionary<Guid, List<Guid>>();
            for (int i = 0; i < chain.Count; i++)
                next[chain[i]] = i + 1 < chain.Count ? new() { chain[i + 1] } : new();

            var components = PathingAlgorithms.StronglyConnectedComponents(next.Keys, id => next[id]);

            Assert.Equal(chain.Count, components.Count);
        }
    }
}
