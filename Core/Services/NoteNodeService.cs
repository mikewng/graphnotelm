using card_library.Core.Application.Repository.Contracts;
using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services
{
    public class NoteNodeService : INoteNodeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INoteGraphAccessService _noteGraphAccessService;

        public NoteNodeService(IUnitOfWork unitOfWork, INoteGraphAccessService noteGraphAccessService)
        {
            _unitOfWork = unitOfWork;
            _noteGraphAccessService = noteGraphAccessService;
        }

        public async Task<Result<CreateNodeResponse>> CreateNodeByGraphId(CreateNodeRequest createNodeRequest, Guid noteGraphId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
            {
                return Result<CreateNodeResponse>.Fail(metadataResult.Error!);
            }

            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
            {
                return Result<CreateNodeResponse>.Fail(graphDataResult.Error!);
            }

            if (createNodeRequest.Title == string.Empty)
            {
                return Result<CreateNodeResponse>.Fail("Failed to create: Title was empty.");
            }

            var graphData = graphDataResult.Value!;
            NoteNode newNode = new NoteNode()
            {
                Id = Guid.NewGuid(),
                Title = createNodeRequest.Title,
                Note = createNodeRequest.Note
            };

            graphData.Nodes[newNode.Id] = newNode;

            //TODO: Persist updated graph document to DynamoDB
            throw new NotImplementedException();
        }

        public async Task<Result<EditNodeResponse>> EditNodeByIds(EditNodeRequest editNodeRequest, Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
            {
                return Result<EditNodeResponse>.Fail(metadataResult.Error!);
            }

            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
            {
                return Result<EditNodeResponse>.Fail(graphDataResult.Error!);
            }

            var graphData = graphDataResult.Value!;
            if (!graphData.Nodes.TryGetValue(noteNodeId, out var existingNode))
            {
                return Result<EditNodeResponse>.Fail("Node not found in graph.");
            }

            existingNode.Title = editNodeRequest.Title;
            existingNode.Note = editNodeRequest.Note;

            //TODO: Persist updated graph document to DynamoDB
            throw new NotImplementedException();
        }

        public async Task<Result<DeleteNodeResponse>> DeleteNodeByIds(Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
            {
                return Result<DeleteNodeResponse>.Fail(metadataResult.Error!);
            }

            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
            {
                return Result<DeleteNodeResponse>.Fail(graphDataResult.Error!);
            }

            var graphData = graphDataResult.Value!;
            if (!graphData.Nodes.ContainsKey(noteNodeId))
            {
                return Result<DeleteNodeResponse>.Fail("Node not found in graph.");
            }

            graphData.Nodes.Remove(noteNodeId);

            //TODO: Persist updated graph document to DynamoDB
            throw new NotImplementedException();
        }

    }
}
