using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Utils;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface IGraphAnalysisService
    {
        // Algorithmic Search Processes
        public GraphView BuildView(NoteGraphDocument document, Guid nodeId);
        public Result<List<Guid>> FindLeastConfidentPath(Guid noteNodeId);
        public Result<List<Guid>> FindKnowledgeFrontier(Guid noteNodeId);
        public Result<List<Guid>> FindBestPathWithBudget(Guid noteNodeId, float budget);

    }
}
