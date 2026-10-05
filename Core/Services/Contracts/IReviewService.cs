using graphnotelm.Core.Models.DTOs;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface IReviewService
    {
        public Task<Result<ReviewSummaryResponse>> GetSummary(Guid noteGraphId, ReviewQueueRequest request, CancellationToken ct);
        public Task<Result<ReviewQueueResponse>> GetQueue(Guid noteGraphId, ReviewQueueRequest request, CancellationToken ct);
        public Task<Result<SubmitReviewResponse>> SubmitReview(SubmitReviewRequest request, Guid noteGraphId, Guid noteNodeId, CancellationToken ct);
    }
}
