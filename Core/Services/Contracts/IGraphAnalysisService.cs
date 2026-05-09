using graphnotelm.Core.Models.DTOs;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface IGraphAnalysisService
    {
        // Algorithmic Search Processes
        public Result<List<Guid>> FindLeastConfidentPath(Guid noteNodeId);
        public Result<List<Guid>> FindKnowledgeFrontier(Guid noteNodeId);
        public Result<List<Guid>> FindBestPathWithBudget(Guid noteNodeId, float budget);

    }
}
