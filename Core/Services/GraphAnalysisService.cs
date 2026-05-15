using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Core.Utils;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services
{
    public class GraphAnalysisService : IGraphAnalysisService
    {
        public GraphView BuildView(NoteGraphDocument document, Guid nodeId)
        {
            return new GraphView(document);
        }

        public Result<List<Guid>> FindLeastConfidentPath(Guid noteNodeId) => throw new NotImplementedException();
        public Result<List<Guid>> FindKnowledgeFrontier(Guid noteNodeId) => throw new NotImplementedException();
        public Result<List<Guid>> FindBestPathWithBudget(Guid noteNodeId, float budget) => throw new NotImplementedException();
    }
}
