using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Utils;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface ILLMAnalysisService
    {
        public Task<Result<EditNodeMetadataResponse>> AnalyzeNodeAsync(Guid noteGraphId, Guid noteNodeId, CancellationToken ct);
        public Task<Result<EditNodeMetadataResponse>> AnalyzeNodeBatchAsync(Guid noteGraphId, List<Guid> noteNodeId);
    }
}
