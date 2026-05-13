using graphnotelm.Core.Models;
using graphnotelm.Core.Utils;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface ILLMAnalysisService
    {
        public Task<Result<bool>> AnalyzeNodeAsync(NoteGraphDocument document, GraphView graph, Guid noteNodeId);
        public Task<Result<bool>> AnalyzeNodeBatchAsync(NoteGraphDocument document, GraphView graph, List<Guid> noteNodeId);
    }
}
