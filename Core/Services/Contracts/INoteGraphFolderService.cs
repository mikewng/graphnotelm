using graphnotelm.Core.Models.DTOs;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface INoteGraphFolderService
    {
        public Task<Result<GetFolderListResponse>> GetFolderListByGraphId(Guid noteGraphId, CancellationToken ct);
        public Task<Result<CreateFolderResponse>> CreateFolderByGraphId(CreateFolderRequest createFolderRequest, Guid noteGraphId, CancellationToken ct);
        public Task<Result<EditFolderResponse>> EditFolderByIds(EditFolderRequest editFolderRequest, Guid noteGraphId, Guid folderId, CancellationToken ct);
        public Task<Result<DeleteFolderResponse>> DeleteFolderByIds(Guid noteGraphId, Guid folderId, CancellationToken ct);
        public Task<Result<MoveNodeToFolderResponse>> MoveNodeToFolder(MoveNodeToFolderRequest request, Guid noteGraphId, Guid noteNodeId, CancellationToken ct);
        public Task<Result<MoveNodesToFolderResponse>> MoveNodesToFolder(MoveNodesToFolderRequest request, Guid noteGraphId, CancellationToken ct);
    }
}
