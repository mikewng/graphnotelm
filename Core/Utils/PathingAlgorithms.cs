using graphnotelm.Core.Models;

namespace graphnotelm.Core.Utils
{
    // Nodes the user already understands, and the nodes just past them that fall below the threshold.
    public record KnowledgeFrontierResult<T>(List<T> Known, List<T> Frontier);

    // Nodes ordered so each comes after its prerequisites. Nodes in a prerequisite cycle,
    // and nodes that depend on one, can't be ordered and are listed in Cyclic instead.
    public record LearningOrderResult<T>(List<T> Order, List<T> Cyclic);

    public class PathingAlgorithms
    {
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
        /// from a node to its prerequisite (e.g. "depends on"). Nodes that are ready at the same
        /// time are ordered by title so the result is stable.
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

            var result = new LearningOrderResult<T>(new List<T>(), new List<T>());
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

            // Every prerequisite of a required node is itself required, so counts stay within the set.
            var unmetCount = new Dictionary<Guid, int>();
            var unlocks = required.ToDictionary(id => id, _ => new List<Guid>());
            foreach (Guid nodeId in required)
            {
                List<Guid> prerequisites = PrerequisitesOf(nodeId);
                unmetCount[nodeId] = prerequisites.Count;
                foreach (Guid prerequisite in prerequisites)
                    unlocks[prerequisite].Add(nodeId);
            }

            var byTitle = Comparer<(string Title, Guid Id)>.Create((a, b) =>
            {
                int compared = StringComparer.OrdinalIgnoreCase.Compare(a.Title, b.Title);
                return compared != 0 ? compared : a.Id.CompareTo(b.Id);
            });
            var ready = new PriorityQueue<Guid, (string Title, Guid Id)>(byTitle);
            void MarkReady(Guid nodeId) => ready.Enqueue(nodeId, (graph.GetNode(nodeId).Title, nodeId));

            foreach (Guid nodeId in required.Where(id => unmetCount[id] == 0))
                MarkReady(nodeId);

            while (ready.TryDequeue(out Guid currNode, out _))
            {
                result.Order.Add(selector(graph.GetNode(currNode)));
                foreach (Guid next in unlocks[currNode])
                {
                    if (--unmetCount[next] == 0)
                        MarkReady(next);
                }
            }

            // Anything still waiting on a prerequisite is stuck behind a cycle.
            result.Cyclic.AddRange(required
                .Where(id => unmetCount[id] > 0)
                .Select(graph.GetNode)
                .OrderBy(node => node.Title, StringComparer.OrdinalIgnoreCase)
                .Select(selector));

            return result;
        }
    }

}
