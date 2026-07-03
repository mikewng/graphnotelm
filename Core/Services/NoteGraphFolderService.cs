using graphnotelm.Infrastructure.Contracts;
using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services
{
    public class NoteGraphFolderService : INoteGraphFolderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INoteGraphAccessService _noteGraphAccessService;
        private readonly INoteGraphRepository _noteGraphRepository;
        private readonly INoteNodeRepository _noteNodeRepository;

        public NoteGraphFolderService(IUnitOfWork unitOfWork, INoteGraphAccessService noteGraphAccessService, INoteGraphRepository noteGraphRepository, INoteNodeRepository noteNodeRepository)
        {
            _unitOfWork = unitOfWork;
            _noteGraphAccessService = noteGraphAccessService;
            _noteGraphRepository = noteGraphRepository;
            _noteNodeRepository = noteNodeRepository;
        }

        public async Task<Result<GetFolderListResponse>> GetFolderListByGraphId(Guid noteGraphId, CancellationToken ct)
        {
            var graphDataResult = await _noteGraphAccessService.GetAuthorizedFullDocumentAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<GetFolderListResponse>.Fail(graphDataResult.Error!);

            return Result<GetFolderListResponse>.Ok(new GetFolderListResponse
            {
                Folders = graphDataResult.Value!.Folders
            });
        }

        public async Task<Result<CreateFolderResponse>> CreateFolderByGraphId(CreateFolderRequest createFolderRequest, Guid noteGraphId, CancellationToken ct)
        {
            var graphDataResult = await _noteGraphAccessService.GetAuthorizedFullDocumentAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<CreateFolderResponse>.Fail(graphDataResult.Error!);

            if (string.IsNullOrWhiteSpace(createFolderRequest.FolderName))
                return Result<CreateFolderResponse>.Fail("Failed to create: Folder name was empty.");

            var graphData = graphDataResult.Value!;
            var folderId = Guid.NewGuid();
            graphData.Folders[folderId] = new FolderDefinition { Name = createFolderRequest.FolderName, Color = createFolderRequest.FolderColor };

            try
            {
                await _noteGraphRepository.SaveAsync(graphData);
                return Result<CreateFolderResponse>.Ok(new CreateFolderResponse { Id = folderId, FolderName = createFolderRequest.FolderName });
            }
            catch
            {
                return Result<CreateFolderResponse>.Fail("Failed to create folder.");
            }
        }

        public async Task<Result<EditFolderResponse>> EditFolderByIds(EditFolderRequest editFolderRequest, Guid noteGraphId, Guid folderId, CancellationToken ct)
        {
            var graphDataResult = await _noteGraphAccessService.GetAuthorizedFullDocumentAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<EditFolderResponse>.Fail(graphDataResult.Error!);

            var graphData = graphDataResult.Value!;
            if (!graphData.Folders.TryGetValue(folderId, out var folder))
                return Result<EditFolderResponse>.Fail("Folder not found.");

            folder.Name = editFolderRequest.FolderName;
            folder.Color = editFolderRequest.FolderColor;

            try
            {
                await _noteGraphRepository.SaveAsync(graphData);
                return Result<EditFolderResponse>.Ok(new EditFolderResponse());
            }
            catch
            {
                return Result<EditFolderResponse>.Fail("Failed to update folder.");
            }
        }

        public async Task<Result<DeleteFolderResponse>> DeleteFolderByIds(Guid noteGraphId, Guid folderId, CancellationToken ct)
        {
            var graphDataResult = await _noteGraphAccessService.GetAuthorizedFullDocumentAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<DeleteFolderResponse>.Fail(graphDataResult.Error!);

            var graphData = graphDataResult.Value!;
            if (!graphData.Folders.TryGetValue(folderId, out var folder))
                return Result<DeleteFolderResponse>.Fail("Folder not found.");

            graphData.Folders.Remove(folderId);

            // Deleting a folder unfiles its members rather than deleting the nodes.
            var affectedNodes = new List<NoteNode>();
            foreach (var node in graphData.Nodes.Values)
            {
                if (node.FolderId == folderId)
                {
                    node.FolderId = null;
                    affectedNodes.Add(node);
                }
            }

            try
            {
                await _noteGraphRepository.SaveAsync(graphData);
                if (affectedNodes.Count > 0)
                    await _noteNodeRepository.SaveManyAsync(noteGraphId, affectedNodes);
                return Result<DeleteFolderResponse>.Ok(new DeleteFolderResponse { FolderName = folder.Name });
            }
            catch
            {
                return Result<DeleteFolderResponse>.Fail("Failed to delete folder.");
            }
        }

        public async Task<Result<MoveNodeToFolderResponse>> MoveNodeToFolder(MoveNodeToFolderRequest request, Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            var graphDataResult = await _noteGraphAccessService.GetAuthorizedFullDocumentAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<MoveNodeToFolderResponse>.Fail(graphDataResult.Error!);

            var graphData = graphDataResult.Value!;
            if (!graphData.Nodes.TryGetValue(noteNodeId, out var node))
                return Result<MoveNodeToFolderResponse>.Fail("Node not found in graph.");
            if (request.FolderId.HasValue && !graphData.Folders.ContainsKey(request.FolderId.Value))
                return Result<MoveNodeToFolderResponse>.Fail("Folder not found in graph.");

            node.FolderId = request.FolderId;

            try
            {
                await _noteNodeRepository.SaveAsync(noteGraphId, node);
                return Result<MoveNodeToFolderResponse>.Ok(new MoveNodeToFolderResponse { NodeId = noteNodeId, FolderId = request.FolderId });
            }
            catch
            {
                return Result<MoveNodeToFolderResponse>.Fail("Failed to move node to folder.");
            }
        }

        public async Task<Result<MoveNodesToFolderResponse>> MoveNodesToFolder(MoveNodesToFolderRequest request, Guid noteGraphId, CancellationToken ct)
        {
            var graphDataResult = await _noteGraphAccessService.GetAuthorizedFullDocumentAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
                return Result<MoveNodesToFolderResponse>.Fail(graphDataResult.Error!);

            var graphData = graphDataResult.Value!;
            if (request.FolderId.HasValue && !graphData.Folders.ContainsKey(request.FolderId.Value))
                return Result<MoveNodesToFolderResponse>.Fail("Folder not found in graph.");

            var response = new MoveNodesToFolderResponse { FolderId = request.FolderId };
            var updatedNodes = new List<NoteNode>();

            foreach (var nodeId in request.NodeIds)
            {
                if (!graphData.Nodes.TryGetValue(nodeId, out var node) || node.FolderId == request.FolderId)
                {
                    response.SkippedNodeIds.Add(nodeId);
                    continue;
                }

                node.FolderId = request.FolderId;
                updatedNodes.Add(node);
                response.UpdatedNodeIds.Add(nodeId);
            }

            try
            {
                if (updatedNodes.Count > 0)
                    await _noteNodeRepository.SaveManyAsync(noteGraphId, updatedNodes);

                return Result<MoveNodesToFolderResponse>.Ok(response);
            }
            catch
            {
                return Result<MoveNodesToFolderResponse>.Fail("Failed to move nodes to folder.");
            }
        }
    }
}
