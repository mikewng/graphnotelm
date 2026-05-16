using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Core.Utils;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services
{
    public class GraphAnalysisService : IGraphAnalysisService
    {
        public GraphView graph;
        public GraphAnalysisService(NoteGraphDocument document) { 
            graph = new GraphView(document);
        }

        public GraphView BuildView(NoteGraphDocument document, Guid nodeId)
        {
            return new GraphView(document);
        }

        public Result<List<Guid>> FindLeastConfidentPath(Guid noteNodeId) { 
            List<Guid> nodes = new List<Guid>();

            List<GuidMetadataPair> lowestScorePath = PathingAlgorithms.DijkstrasById(noteNodeId, graph);

            foreach (GuidMetadataPair pair in lowestScorePath)
            {
                nodes.Add(pair.Id);
            }

            return Result<List<Guid>>.Ok(nodes);
        }

        public Result<List<Guid>> FindKnowledgeFrontier(Guid noteNodeId) {
            List<Guid> nodes = PathingAlgorithms.BreadthFirstSearchById(noteNodeId, 3.0f, graph);
            return Result<List<Guid>>.Ok(nodes);
        }
        public Result<List<Guid>> FindBestPathWithBudget(Guid noteNodeId, float budget) => throw new NotImplementedException();
    }
}
