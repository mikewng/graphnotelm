using graphnotelm.Core.Models.DTOs;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface IGraphRelationshipService
    {
        public Task<Result<GetRelationshipListResponse>> GetRelationshipListByGraphId(Guid noteGraphId, CancellationToken ct);
        public Task<Result<CreateRelationshipResponse>> CreateRelationshipByGraphId(CreateRelationshipRequest createRelationshipRequest, Guid noteGraphId, CancellationToken ct);
        public Task<Result<EditRelationshipResponse>> EditRelationshipByIds(EditRelationshipRequest editRelationshipRequest, Guid noteGraphId, Guid relationId, CancellationToken ct);
        public Task<Result<DeleteRelationshipResponse>> DeleteRelationshipByIds(Guid noteGraphId, Guid relationId, CancellationToken ct);
    }
}
