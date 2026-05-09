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
        public static List<GuidMetadataPair> DijkstrasById(Guid noteNodeId, GraphView graph)
        {
            List<GuidMetadataPair> path = new List<GuidMetadataPair>();

            NoteNode startingNode = graph.GetNode(noteNodeId);

            HashSet<Guid> visited = new HashSet<Guid>();
            Queue<Guid> queue = new Queue<Guid>();

            visited.Add(noteNodeId);
            queue.Enqueue(noteNodeId);

            while (queue.Count > 0)
            {
                Guid currNode = queue.Dequeue();
                List<Guid> neighbors = graph.GetNeighbors(currNode);

                float currentMinDistance = int.MaxValue;
                Guid currentMinNode = Guid.Empty;
                string currentMinNodeMetadata = string.Empty;

                foreach (Guid neighbor in neighbors)
                {
                    if (!visited.Contains(neighbor))
                    {
                        NoteNode currNeighbor = graph.GetNode(neighbor);
                        float currentDistance = currNeighbor.Metadata.UserConfidenceRate;
                        string currentMetadata = currNeighbor.Metadata.LLMMetadata;

                        if (currentDistance <= currentMinDistance)
                        {
                            currentMinDistance = currentDistance;
                            currentMinNode = neighbor;
                            currentMinNodeMetadata = currentMetadata;
                        }
                    }
                }
                if (currentMinNode != Guid.Empty)
                {
                    path.Add(new GuidMetadataPair() { Id = currentMinNode, Metadata = currentMinNodeMetadata});

                    queue.Enqueue(currentMinNode);
                    visited.Add(currentMinNode);
                }
                if (graph.GetNeighbors(currentMinNode).Count == 0)
                {
                    break;
                }
            }

            return path;
        }

        public static List<Guid> BreadthFirstSearchById(Guid noteNodeId, float minConfidence, GraphView graph)
        {
            List<Guid> nodes = new List<Guid>() { noteNodeId };

            NoteNode startingNode = graph.GetNode(noteNodeId);

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
                    if (!visited.Contains(neighbor))
                    {
                        NoteNode currNeighbor = graph.GetNode(neighbor);
                        float currentConfidenceRate = currNeighbor.Metadata.UserConfidenceRate;

                        if (currentConfidenceRate >= minConfidence)
                        {
                            queue.Enqueue(neighbor);
                            visited.Add(neighbor);
                            nodes.Add(neighbor);
                        } else
                        {
                            continue;
                        }
                    }
                }
            }

            return nodes;
        }
    }

}
