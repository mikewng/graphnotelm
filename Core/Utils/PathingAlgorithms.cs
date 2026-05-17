using graphnotelm.Core.Models;

namespace graphnotelm.Core.Utils
{
    public class GuidMetadataPair()
    {
        public Guid Id { get; set; }
        public string Metadata { get; set; } = string.Empty;
    }

    public class PathingAlgorithms
    {
        public static List<T> DijkstrasById<T>(Guid noteNodeId, GraphView graph, Func<NoteNode, T> selector)
        {
            var pq = new PriorityQueue<Guid, float>();
            var dist = new Dictionary<Guid, float>();
            var visited = new HashSet<Guid>();
            var result = new List<T>();

            foreach (var id in graph.AllNodes.Keys)
                dist[id] = float.MaxValue;

            dist[noteNodeId] = 0f;
            pq.Enqueue(noteNodeId, 0f);

            while (pq.Count > 0)
            {
                Guid curr = pq.Dequeue();
                if (!visited.Add(curr)) continue;

                NoteNode node = graph.GetNode(curr);
                result.Add(selector(node));

                foreach (Guid neighbor in graph.GetNeighbors(curr))
                {
                    if (visited.Contains(neighbor)) continue;
                    float newDist = dist[curr] + graph.GetNode(neighbor).Metadata.UserConfidenceRate;
                    if (newDist < dist[neighbor])
                    {
                        dist[neighbor] = newDist;
                        pq.Enqueue(neighbor, newDist);
                    }
                }
            }

            return result;
        }

        public static List<T> BreadthFirstSearchById<T>(Guid noteNodeId, float minConfidence, GraphView graph, Func<NoteNode, T> selector)
        {
            NoteNode startNode = graph.GetNode(noteNodeId);
            List<T> nodes = new List<T>() { selector(startNode) };

            HashSet<Guid> visited = new HashSet<Guid>();
            Queue<Guid> queue = new Queue<Guid>();

            visited.Add(noteNodeId);
            queue.Enqueue(noteNodeId);

            while (queue.Count > 0)
            {
                Guid currNode = queue.Dequeue();
                List<Guid> neighbors = graph.GetNeighbors(currNode);

                foreach (Guid neighbor in neighbors)
                {
                    if (!visited.Add(neighbor)) continue;

                    NoteNode currNeighbor = graph.GetNode(neighbor);
                    if (currNeighbor.Metadata.UserConfidenceRate >= minConfidence)
                    {
                        queue.Enqueue(neighbor);
                        nodes.Add(selector(currNeighbor));
                    }
                }
            }

            return nodes;
        }
    }

}
