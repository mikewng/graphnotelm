using graphnotelm.Core.Models.DTOs;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface INoteNodeService
    {
        public Task<Result<GetNodeResponse>> GetNodeByIds(Guid noteGraphId, Guid noteNodeId, CancellationToken ct);
        public Task<Result<GetNodeBatchResponse>> GetNodeBatchByIds(Guid noteGraphId, List<Guid> nodeIds, CancellationToken ct);
        public Task<Result<CreateNodeResponse>> CreateNodeByGraphId(CreateNodeRequest createNodeRequest, Guid noteGraphId, CancellationToken ct);
        public Task<Result<EditNodeResponse>> EditNodeByIds(EditNodeRequest editNodeRequest, Guid noteGraphId, Guid noteNodeId, CancellationToken ct);
        public Task<Result<DeleteNodeResponse>> DeleteNodeByIds(Guid noteGraphId, Guid noteNodeId, CancellationToken ct);
        public Task<Result<SaveNodeContentResponse>> SaveNodeContentAsync(SaveNodeContentRequest saveNodeContentRequest, Guid noteGraphId, Guid noteNodeId, CancellationToken ct);
        public Task<Result<EditNodeMetadataResponse>> EditNodeMetadataByIds(EditNodeMetadataRequest editNodeMetadataRequest, Guid noteGraphId, Guid noteNodeId, CancellationToken ct);
        public Task<Result<CreateNodeResponse>> CreateNodeFromPastedContent(CreateNotePastedRequest createNotePastedRequest, Guid noteGraphId, CancellationToken ct);
        public Task<Result<SearchNodesResponse>> SearchNodesByContent(Guid noteGraphId, string query, CancellationToken ct);
        public Task<Result<GetPinnedNodesResponse>> GetPinnedNodes(Guid noteGraphId, CancellationToken ct);
        public Task<Result<SetPinnedResponse>> SetNodePinned(Guid noteGraphId, Guid noteNodeId, bool isPinned, CancellationToken ct);
    }
}
