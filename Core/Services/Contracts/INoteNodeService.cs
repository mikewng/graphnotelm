using graphnotelm.Core.Models.DTOs;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface INoteNodeService
    {
        public Task<Result<CreateNodeResponse>> CreateNodeByGraphId(CreateNodeRequest createNodeRequest, Guid noteGraphId, CancellationToken ct);
        public Task<Result<EditNodeResponse>> EditNodeByIds(EditNodeRequest editNodeRequest, Guid noteGraphId, Guid noteNodeId, CancellationToken ct);
        public Task<Result<DeleteNodeResponse>> DeleteNodeByIds(Guid noteGraphId, Guid noteNodeId, CancellationToken ct);
    }
}
