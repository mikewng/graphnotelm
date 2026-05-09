using graphnotelm.Core.Models.DTOs;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface INoteGraphTagService
    {
        public Task<Result<GetTagListResponse>> GetTagListByGraphId(Guid noteGraphId, CancellationToken ct);
        public Task<Result<CreateTagResponse>> CreateTagByGraphId(CreateTagRequest createTagRequest, Guid noteGraphId, CancellationToken ct);
        public Task<Result<EditTagResponse>> EditTagByIds(EditTagRequest editTagRequest, Guid noteGraphId, Guid tagId, CancellationToken ct);
        public Task<Result<DeleteTagResponse>> DeleteTagByIds(Guid noteGraphId, Guid tagId, CancellationToken ct);
        public Task<Result<AddNodeTagResponse>> AddTagToNode(AddNodeTagRequest request, Guid noteGraphId, Guid noteNodeId, CancellationToken ct);
        public Task<Result<RemoveNodeTagResponse>> RemoveTagFromNode(Guid noteGraphId, Guid noteNodeId, Guid tagId, CancellationToken ct);
    }
}
