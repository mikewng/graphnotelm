using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Utils;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface IGraphAnalysisService
    {
        public GraphView BuildView(NoteGraphDocument document, Guid nodeId);

        // Algorithmic Search Processes
        public Task<Result<WeakestPathResponse>> FindWeakestPath(Guid noteGraphId, WeakestPathRequest request, CancellationToken ct);
        public Task<Result<KnowledgeFrontierResponse>> FindKnowledgeFrontier(Guid noteGraphId, KnowledgeFrontierRequest request, CancellationToken ct);
        public Task<Result<LearningOrderResponse>> FindLearningOrder(Guid noteGraphId, LearningOrderRequest request, CancellationToken ct);
    }
}
