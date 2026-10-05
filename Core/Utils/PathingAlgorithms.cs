using graphnotelm.Core.Models;

namespace graphnotelm.Core.Utils
{
    // Nodes the user already understands, and the nodes just past them that fall below the threshold.
    public record KnowledgeFrontierResult<T>(List<T> Known, List<T> Frontier);

    // Nodes ordered so each comes after its prerequisites. Nodes in a prerequisite cycle can't
    // each come after the others, so every cycle stays together in Order and is also listed in Cycles.
    public record LearningOrderResult<T>(List<T> Order, List<List<T>> Cycles);

    public class PathingAlgorithms
    {
        // Confidence runs from 0 to 10.
        private const float FullConfidence = 10f;

        /// <summary>
        /// Finds the path from start to target whose nodes have the lowest total confidence —
        /// the hardest route to the target. Returns an empty list when the target can't be reached.
        /// </summary>
        public static List<T> DijkstrasById<T>(
            Guid startNodeId,
            Guid targetNodeId,
            GraphView graph,
            Func<NoteNode, T> selector,
            EdgeDirection direction = EdgeDirection.Both,
            IReadOnlySet<Guid>? relationshipIds = null)
        {
            if (!graph.HasNode(startNodeId) || !graph.HasNode(targetNodeId))
                return new List<T>();

            // A path costs the summed confidence of the nodes it enters; equal costs go to the
            // path with fewer hops.
            var best = new Dictionary<Guid, (float Cost, int Hops)> { [startNodeId] = (0f, 0) };
            var previous = new Dictionary<Guid, Guid>();
            var visited = new HashSet<Guid>();
            var pq = new PriorityQueue<Guid, (float Cost, int Hops)>();
            pq.Enqueue(startNodeId, (0f, 0));

            while (pq.TryDequeue(out Guid curr, out var currBest))
            {
                if (!visited.Add(curr)) continue;
                if (curr == targetNodeId) break;

                foreach (Guid neighbor in graph.GetNeighbors(curr, direction, relationshipIds))
                {
                    if (visited.Contains(neighbor)) continue;

                    // Dijkstra needs non-negative costs, and confidence isn't range-checked on save.
                    float cost = Math.Max(0f, graph.GetConfidence(neighbor));
                    var candidate = (currBest.Cost + cost, currBest.Hops + 1);
                    if (!best.TryGetValue(neighbor, out var known) || candidate.CompareTo(known) < 0)
                    {
                        best[neighbor] = candidate;
                        previous[neighbor] = curr;
                        pq.Enqueue(neighbor, candidate);
                    }
                }
            }

            if (!visited.Contains(targetNodeId))
                return new List<T>();

            var path = new List<T>();
            for (Guid id = targetNodeId; ; id = previous[id])
            {
                path.Add(selector(graph.GetNode(id)));
                if (id == startNodeId) break;
            }
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Walks out from the start node through nodes at or above minConfidence — what the user
        /// already knows. The nodes reached that fall below it are the frontier: what to learn next.
        /// A start node below the threshold is itself the frontier.
        /// </summary>
        public static KnowledgeFrontierResult<T> BreadthFirstSearchById<T>(
            Guid startNodeId,
            float minConfidence,
            GraphView graph,
            Func<NoteNode, T> selector,
            EdgeDirection direction = EdgeDirection.Both,
            IReadOnlySet<Guid>? relationshipIds = null)
        {
            var result = new KnowledgeFrontierResult<T>(new List<T>(), new List<T>());
            if (!graph.HasNode(startNodeId))
                return result;

            HashSet<Guid> visited = new HashSet<Guid>() { startNodeId };
            Queue<Guid> queue = new Queue<Guid>();

            // Known nodes are explored further; frontier nodes are where the search stops.
            void Classify(Guid nodeId)
            {
                NoteNode node = graph.GetNode(nodeId);
                if (graph.GetConfidence(nodeId) >= minConfidence)
                {
                    result.Known.Add(selector(node));
                    queue.Enqueue(nodeId);
                }
                else
                {
                    result.Frontier.Add(selector(node));
                }
            }

            Classify(startNodeId);
            while (queue.TryDequeue(out Guid currNode))
            {
                foreach (Guid neighbor in graph.GetNeighbors(currNode, direction, relationshipIds))
                {
                    if (visited.Add(neighbor))
                        Classify(neighbor);
                }
            }

            return result;
        }

        /// <summary>
        /// Orders the target node and everything it transitively depends on so that each node
        /// comes after its prerequisites (Kahn's algorithm). With Outgoing, an edge points from a
        /// prerequisite to the node that needs it (e.g. "prerequisite to"); with Incoming, it points
        /// from a node to its prerequisite (e.g. "depends on"). Nodes in a prerequisite cycle are
        /// ordered as one group, after every prerequisite outside the cycle, and the group is also
        /// listed in Cycles. Nodes and groups that are ready at the same time are ordered by title
        /// so the result is stable.
        /// </summary>
        public static LearningOrderResult<T> KahnTopologicalSortById<T>(
            Guid targetNodeId,
            GraphView graph,
            Func<NoteNode, T> selector,
            IReadOnlySet<Guid>? relationshipIds = null,
            EdgeDirection direction = EdgeDirection.Outgoing)
        {
            if (direction == EdgeDirection.Both)
                throw new ArgumentException("A learning order needs edges that point one way.", nameof(direction));

            var result = new LearningOrderResult<T>(new List<T>(), new List<List<T>>());
            if (!graph.HasNode(targetNodeId))
                return result;

            var towardPrerequisites = direction == EdgeDirection.Outgoing ? EdgeDirection.Incoming : EdgeDirection.Outgoing;
            List<Guid> PrerequisitesOf(Guid nodeId) => graph.GetNeighbors(nodeId, towardPrerequisites, relationshipIds);

            // The target plus every node it transitively depends on.
            var required = new HashSet<Guid>() { targetNodeId };
            var stack = new Stack<Guid>();
            stack.Push(targetNodeId);
            while (stack.TryPop(out Guid currNode))
            {
                foreach (Guid prerequisite in PrerequisitesOf(currNode))
                {
                    if (required.Add(prerequisite))
                        stack.Push(prerequisite);
                }
            }

            var byTitle = Comparer<(string Title, Guid Id)>.Create((a, b) =>
            {
                int compared = StringComparer.OrdinalIgnoreCase.Compare(a.Title, b.Title);
                return compared != 0 ? compared : a.Id.CompareTo(b.Id);
            });

            // Collapse each cycle into one group so the groups' prerequisites can't loop and every
            // group can be ordered. A group's members are sorted by title, and its first member places it.
            var groups = StronglyConnectedComponents(required, PrerequisitesOf)
                .Select(members => members.OrderBy(id => (graph.GetNode(id).Title, id), byTitle).ToList())
                .ToList();
            var groupOf = new Dictionary<Guid, int>();
            for (int group = 0; group < groups.Count; group++)
            {
                foreach (Guid nodeId in groups[group])
                    groupOf[nodeId] = group;
            }

            // Every prerequisite of a required node is itself required, so counts stay within the set.
            var unmetCount = new int[groups.Count];
            var unlocks = groups.Select(_ => new HashSet<int>()).ToArray();
            foreach (Guid nodeId in required)
            {
                int group = groupOf[nodeId];
                foreach (Guid prerequisite in PrerequisitesOf(nodeId))
                {
                    int before = groupOf[prerequisite];
                    if (before != group && unlocks[before].Add(group))
                        unmetCount[group]++;
                }
            }

            var ready = new PriorityQueue<int, (string Title, Guid Id)>(byTitle);
            void MarkReady(int group) => ready.Enqueue(group, (graph.GetNode(groups[group][0]).Title, groups[group][0]));

            for (int group = 0; group < groups.Count; group++)
            {
                if (unmetCount[group] == 0)
                    MarkReady(group);
            }

            while (ready.TryDequeue(out int currGroup, out _))
            {
                List<Guid> members = groups[currGroup];
                List<T> selected = members.Select(id => selector(graph.GetNode(id))).ToList();
                result.Order.AddRange(selected);

                // A lone node is a cycle only when it lists itself as a prerequisite.
                if (members.Count > 1 || PrerequisitesOf(members[0]).Contains(members[0]))
                    result.Cycles.Add(selected);

                foreach (int next in unlocks[currGroup])
                {
                    if (--unmetCount[next] == 0)
                        MarkReady(next);
                }
            }

            return result;
        }

        /// <summary>
        /// Finds the nodes below minConfidence whose prerequisites are all at or above it — what the
        /// user can start on now (the outer fringe, in knowledge space theory). Nodes in a prerequisite
        /// cycle are learned together, so they're ready once every prerequisite outside the cycle is
        /// known. Nodes with no edges of the followed types aren't part of any prerequisite chain and
        /// are left out. Edge direction works as in KahnTopologicalSortById. Ordered by title.
        /// </summary>
        public static List<T> FindReadyToLearn<T>(
            float minConfidence,
            GraphView graph,
            Func<NoteNode, T> selector,
            IReadOnlySet<Guid>? relationshipIds = null,
            EdgeDirection direction = EdgeDirection.Outgoing)
        {
            if (direction == EdgeDirection.Both)
                throw new ArgumentException("Prerequisites need edges that point one way.", nameof(direction));

            var towardPrerequisites = direction == EdgeDirection.Outgoing ? EdgeDirection.Incoming : EdgeDirection.Outgoing;
            var prerequisitesOf = graph.AllNodes.Keys.ToDictionary(id => id,
                id => graph.GetNeighbors(id, towardPrerequisites, relationshipIds));
            bool IsKnown(Guid nodeId) => graph.GetConfidence(nodeId) >= minConfidence;
            bool IsLinked(Guid nodeId) => prerequisitesOf[nodeId].Count > 0
                || graph.GetNeighbors(nodeId, direction, relationshipIds).Count > 0;

            var ready = new List<Guid>();
            foreach (List<Guid> group in StronglyConnectedComponents(prerequisitesOf.Keys, id => prerequisitesOf[id]))
            {
                var members = group.ToHashSet();
                bool unlocked = group.All(id => prerequisitesOf[id].All(prerequisite =>
                    members.Contains(prerequisite) || IsKnown(prerequisite)));
                if (unlocked)
                    ready.AddRange(group.Where(id => !IsKnown(id) && IsLinked(id)));
            }

            return ready
                .Select(graph.GetNode)
                .OrderBy(node => node.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(node => node.Id)
                .Select(selector)
                .ToList();
        }

        /// <summary>
        /// Ranks the nodes that hold back the most: how many nodes depend on each one, directly or
        /// through others, times how far its confidence is from full. A shaky note that much of the
        /// graph builds on outranks a weaker one that little depends on. Nodes nothing depends on,
        /// and fully known nodes, aren't bottlenecks. Edge direction works as in
        /// KahnTopologicalSortById. The selector also gets the node's dependent count.
        /// </summary>
        public static List<T> FindBottlenecks<T>(
            GraphView graph,
            Func<NoteNode, int, T> selector,
            int limit,
            IReadOnlySet<Guid>? relationshipIds = null,
            EdgeDirection direction = EdgeDirection.Outgoing)
        {
            if (direction == EdgeDirection.Both)
                throw new ArgumentException("Prerequisites need edges that point one way.", nameof(direction));

            // Each node's dependents are walked once per search, so read them once up front.
            var dependentsOf = graph.AllNodes.Keys.ToDictionary(id => id,
                id => graph.GetNeighbors(id, direction, relationshipIds));

            // One search per node keeps counts exact; summing dependents' own counts would count
            // a node reached along two routes twice.
            int CountDependents(Guid nodeId)
            {
                var reached = new HashSet<Guid>();
                var stack = new Stack<Guid>();
                stack.Push(nodeId);
                while (stack.TryPop(out Guid currNode))
                {
                    foreach (Guid dependent in dependentsOf[currNode])
                    {
                        if (reached.Add(dependent))
                            stack.Push(dependent);
                    }
                }

                // A cycle can lead back to the start, but a node isn't its own dependent.
                reached.Remove(nodeId);
                return reached.Count;
            }

            return graph.AllNodes.Values
                .Select(node => (
                    Node: node,
                    Dependents: CountDependents(node.Id),
                    // Confidence isn't range-checked on save.
                    Weakness: Math.Clamp(FullConfidence - graph.GetConfidence(node.Id), 0f, FullConfidence)))
                .Where(n => n.Dependents > 0 && n.Weakness > 0)
                .OrderByDescending(n => n.Dependents * n.Weakness)
                .ThenByDescending(n => n.Dependents)
                .ThenBy(n => n.Node.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(n => n.Node.Id)
                .Take(limit)
                .Select(n => selector(n.Node, n.Dependents))
                .ToList();
        }

        /// <summary>
        /// Splits nodes into groups where every node can reach every other by following next: each
        /// cycle forms one group, and a node on no cycle is a group of its own (Tarjan's algorithm).
        /// A group comes after every group reachable from it.
        /// </summary>
        public static List<List<Guid>> StronglyConnectedComponents(IEnumerable<Guid> nodeIds, Func<Guid, List<Guid>> next)
        {
            var components = new List<List<Guid>>();
            var visitOrder = new Dictionary<Guid, int>();
            // The earliest-visited node, still waiting for its group, that each node can reach.
            var lowLink = new Dictionary<Guid, int>();
            var waiting = new Stack<Guid>();
            var isWaiting = new HashSet<Guid>();
            // Depth-first search on an explicit stack, so a long prerequisite chain can't overflow the call stack.
            var path = new Stack<(Guid Node, IEnumerator<Guid> Next)>();
            int visited = 0;

            void Visit(Guid nodeId)
            {
                visitOrder[nodeId] = lowLink[nodeId] = visited++;
                waiting.Push(nodeId);
                isWaiting.Add(nodeId);
                path.Push((nodeId, next(nodeId).GetEnumerator()));
            }

            foreach (Guid root in nodeIds)
            {
                if (visitOrder.ContainsKey(root))
                    continue;

                Visit(root);
                while (path.TryPeek(out var frame))
                {
                    var (currNode, successors) = frame;
                    if (successors.MoveNext())
                    {
                        Guid successor = successors.Current;
                        if (!visitOrder.ContainsKey(successor))
                            Visit(successor);
                        else if (isWaiting.Contains(successor))
                            lowLink[currNode] = Math.Min(lowLink[currNode], visitOrder[successor]);
                        continue;
                    }

                    path.Pop();
                    if (path.TryPeek(out var parent))
                        lowLink[parent.Node] = Math.Min(lowLink[parent.Node], lowLink[currNode]);

                    // Nothing currNode reaches leads back before it, so it and every node still
                    // waiting above it form one group.
                    if (lowLink[currNode] == visitOrder[currNode])
                    {
                        var component = new List<Guid>();
                        Guid member;
                        do
                        {
                            member = waiting.Pop();
                            isWaiting.Remove(member);
                            component.Add(member);
                        } while (member != currNode);
                        components.Add(component);
                    }
                }
            }

            return components;
        }
    }

}
