using graphnotelm.Infrastructure.Contracts;
using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services
{
    public class NoteNodeService : INoteNodeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INoteGraphAccessService _noteGraphAccessService;
        private readonly INoteGraphRepository _noteGraphRepository;

        public NoteNodeService(IUnitOfWork unitOfWork, INoteGraphAccessService noteGraphAccessService, INoteGraphRepository noteGraphRepository)
        {
            _unitOfWork = unitOfWork;
            _noteGraphAccessService = noteGraphAccessService;
            _noteGraphRepository = noteGraphRepository;
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

            try
            {
                await _noteGraphRepository.SaveAsync(graphData);
                return Result<CreateNodeResponse>.Ok(new CreateNodeResponse { Id = newNode.Id, Title = newNode.Title });
            }
            catch
            {
                return Result<CreateNodeResponse>.Fail("Failed to create node.");
            }
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

            var invalidTagIds = editNodeRequest.Tags.Where(tagId => !graphData.Tags.ContainsKey(tagId)).ToList();
            if (invalidTagIds.Any())
            {
                return Result<EditNodeResponse>.Fail($"Tag(s) not found in graph: {string.Join(", ", invalidTagIds)}");
            }

            foreach (var rel in editNodeRequest.Relationships)
            {
                if (rel.TargetNodeId == noteNodeId)
                    return Result<EditNodeResponse>.Fail("A node cannot have a relationship with itself.");
                if (!graphData.Nodes.ContainsKey(rel.TargetNodeId))
                    return Result<EditNodeResponse>.Fail($"Target node not found: {rel.TargetNodeId}");
                if (!graphData.Relationships.ContainsKey(rel.RelationshipId))
                    return Result<EditNodeResponse>.Fail($"Relationship type not found: {rel.RelationshipId}");
            }

            existingNode.Tags = editNodeRequest.Tags;
            existingNode.Relationships = editNodeRequest.Relationships;

            try
            {
                await _noteGraphRepository.SaveAsync(graphData);
                return Result<EditNodeResponse>.Ok(new EditNodeResponse { NoteNodeContent = existingNode });
            }
            catch
            {
                return Result<EditNodeResponse>.Fail("Failed to update node.");
            }
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
            foreach (var node in graphData.Nodes.Values)
            {
                node.Relationships.RemoveAll(r => r.TargetNodeId == noteNodeId);
            }

            try
            {
                await _noteGraphRepository.SaveAsync(graphData);
                return Result<DeleteNodeResponse>.Ok(new DeleteNodeResponse { Id = noteNodeId, IsDeleted = true });
            }
            catch
            {
                return Result<DeleteNodeResponse>.Fail("Failed to delete node.");
            }
        }

        public async Task<Result<SaveNodeContentResponse>> SaveNodeContentAsync(SaveNodeContentRequest saveNodeContentRequest, Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
            {
                return Result<SaveNodeContentResponse>.Fail(metadataResult.Error!);
            }

            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
            {
                return Result<SaveNodeContentResponse>.Fail(graphDataResult.Error!);
            }

            var graphData = graphDataResult.Value!;
            if (!graphData.Nodes.TryGetValue(noteNodeId, out var existingNode))
            {
                return Result<SaveNodeContentResponse>.Fail("Node not found in graph.");
            }

            if (saveNodeContentRequest.Title is not null)
            {
                if (saveNodeContentRequest.Title == string.Empty)
                    return Result<SaveNodeContentResponse>.Fail("Title cannot be empty.");
                existingNode.Title = saveNodeContentRequest.Title;
            }

            if (saveNodeContentRequest.Note is not null)
                existingNode.Note = saveNodeContentRequest.Note;

            try
            {
                await _noteGraphRepository.SaveAsync(graphData);
                return Result<SaveNodeContentResponse>.Ok(new SaveNodeContentResponse { IsSuccess = true });
            }
            catch
            {
                return Result<SaveNodeContentResponse>.Fail("Failed to save node content.");
            }
        }

        public async Task<Result<EditNodeMetadataResponse>> EditNodeMetadataByIds(EditNodeMetadataRequest editNodeMetadataRequest, Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
            {
                return Result<EditNodeMetadataResponse>.Fail(metadataResult.Error!);
            }

            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
            {
                return Result<EditNodeMetadataResponse>.Fail(graphDataResult.Error!);
            }

            var graphData = graphDataResult.Value!;
            if (!graphData.Nodes.TryGetValue(noteNodeId, out var existingNode))
            {
                return Result<EditNodeMetadataResponse>.Fail("Node not found in graph.");
            }

            if (editNodeMetadataRequest.UserConfidenceRate.HasValue)
                existingNode.Metadata.UserConfidenceRate = editNodeMetadataRequest.UserConfidenceRate.Value;
            if (editNodeMetadataRequest.LLMMetadata is not null)
                existingNode.Metadata.LLMMetadata = editNodeMetadataRequest.LLMMetadata;

            try
            {
                await _noteGraphRepository.SaveAsync(graphData);
                return Result<EditNodeMetadataResponse>.Ok(new EditNodeMetadataResponse
                {
                    NodeId = noteNodeId,
                    Metadata = existingNode.Metadata
                });
            }
            catch
            {
                return Result<EditNodeMetadataResponse>.Fail("Failed to update node metadata.");
            }
        }
    }
}
