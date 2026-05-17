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

        public Result<List<string>> FindLeastConfidentPath(Guid noteNodeId) {
            return Result<List<string>>.Ok(new List<string>() { "none - this method is to be implemented." });
        }
        public Result<List<string>> FindKnowledgeFrontier(Guid noteNodeId) {
            return Result<List<string>>.Ok(new List<string>() { "none - this method is to be implemented." });
        }
        public Result<List<string>> FindBestPathWithBudget(Guid noteNodeId, float budget) {
            return Result<List<string>>.Ok(new List<string>() { "none - this method is to be implemented." });
        }
    }
}
