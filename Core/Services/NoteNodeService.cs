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
        private readonly INoteNodeRepository _noteNodeRepository;
        private readonly ILLMAnalysisService _llmAnalysisService;

        public NoteNodeService(IUnitOfWork unitOfWork, INoteGraphAccessService noteGraphAccessService, INoteGraphRepository noteGraphRepository, INoteNodeRepository noteNodeRepository, ILLMAnalysisService llmAnalysisService)
        {
            _unitOfWork = unitOfWork;
            _noteGraphAccessService = noteGraphAccessService;
            _noteGraphRepository = noteGraphRepository;
            _noteNodeRepository = noteNodeRepository;
            _llmAnalysisService = llmAnalysisService;
        }

        public async Task<Result<GetNodeResponse>> GetNodeByIds(Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return Result<GetNodeResponse>.Fail(metadataResult.Error!);

            var node = await _noteNodeRepository.GetByIdAsync(noteGraphId, noteNodeId, ct);
            if (node is null)
                return Result<GetNodeResponse>.Fail("Node not found.");

            return Result<GetNodeResponse>.Ok(new GetNodeResponse
            {
                Id = node.Id,
                Title = node.Title,
                Note = node.Note,
                Metadata = node.Metadata,
                Relationships = node.Relationships,
                Tags = node.Tags
            });
        }

        public async Task<Result<GetNodeBatchResponse>> GetNodeBatchByIds(Guid noteGraphId, List<Guid> nodeIds, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return Result<GetNodeBatchResponse>.Fail(metadataResult.Error!);

            var nodes = new Dictionary<Guid, GetNodeResponse>();
            foreach (var nodeId in nodeIds)
            {
                var node = await _noteNodeRepository.GetByIdAsync(noteGraphId, nodeId, ct);
                if (node is not null)
                    nodes[node.Id] = new GetNodeResponse
                    {
                        Id = node.Id,
                        Title = node.Title,
                        Note = node.Note,
                        Metadata = node.Metadata,
                        Relationships = node.Relationships,
                        Tags = node.Tags
                    };
            }

            return Result<GetNodeBatchResponse>.Ok(new GetNodeBatchResponse { Nodes = nodes });
        }

        public async Task<Result<CreateNodeResponse>> CreateNodeByGraphId(CreateNodeRequest createNodeRequest, Guid noteGraphId, CancellationToken ct)
        {
            var graphDataResult = await _noteGraphAccessService.GetAuthorizedFullDocumentAsync(noteGraphId, ct);
            if (!graphDataResult.Success)
            {
                return Result<CreateNodeResponse>.Fail(graphDataResult.Error!);
            }

            if (createNodeRequest.Title == string.Empty)
            {
                return Result<CreateNodeResponse>.Fail("Failed to create: Title was empty.");
            }

            NoteNode newNode = new NoteNode()
            {
                Id = Guid.NewGuid(),
                Title = createNodeRequest.Title,
                Note = createNodeRequest.Note
            };

            try
            {
                await _noteNodeRepository.SaveAsync(noteGraphId, newNode);
                return Result<CreateNodeResponse>.Ok(new CreateNodeResponse { Id = newNode.Id, Title = newNode.Title });
            }
            catch
            {
                return Result<CreateNodeResponse>.Fail("Failed to create node.");
            }
        }

        public async Task<Result<EditNodeResponse>> EditNodeByIds(EditNodeRequest editNodeRequest, Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            var graphDataResult = await _noteGraphAccessService.GetAuthorizedFullDocumentAsync(noteGraphId, ct);
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
                await _noteNodeRepository.SaveAsync(noteGraphId, existingNode);
                return Result<EditNodeResponse>.Ok(new EditNodeResponse { NoteNodeContent = existingNode });
            }
            catch
            {
                return Result<EditNodeResponse>.Fail("Failed to update node.");
            }
        }

        public async Task<Result<DeleteNodeResponse>> DeleteNodeByIds(Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            var graphDataResult = await _noteGraphAccessService.GetAuthorizedFullDocumentAsync(noteGraphId, ct);
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

            var affectedNodes = new List<NoteNode>();
            foreach (var node in graphData.Nodes.Values)
            {
                if (node.Relationships.RemoveAll(r => r.TargetNodeId == noteNodeId) > 0)
                    affectedNodes.Add(node);
            }

            try
            {
                await _noteNodeRepository.DeleteAsync(noteGraphId, noteNodeId);
                foreach (var node in affectedNodes)
                    await _noteNodeRepository.SaveAsync(noteGraphId, node);
                return Result<DeleteNodeResponse>.Ok(new DeleteNodeResponse { Id = noteNodeId, IsDeleted = true });
            }
            catch
            {
                return Result<DeleteNodeResponse>.Fail("Failed to delete node.");
            }
        }

        public async Task<Result<SaveNodeContentResponse>> SaveNodeContentAsync(SaveNodeContentRequest saveNodeContentRequest, Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            var graphDataResult = await _noteGraphAccessService.GetAuthorizedFullDocumentAsync(noteGraphId, ct);
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
                await _noteNodeRepository.SaveAsync(noteGraphId, existingNode);
                return Result<SaveNodeContentResponse>.Ok(new SaveNodeContentResponse { IsSuccess = true });
            }
            catch
            {
                return Result<SaveNodeContentResponse>.Fail("Failed to save node content.");
            }
        }

        public async Task<Result<CreateNodeResponse>> CreateNodeFromPastedContent(CreateNotePastedRequest createNotePastedRequest, Guid noteGraphId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(createNotePastedRequest.PastedContent))
                return Result<CreateNodeResponse>.Fail("Pasted content was empty.");

            var extractResult = await _llmAnalysisService.ExtractNodeFromPasteAsync(noteGraphId, createNotePastedRequest.PastedContent, ct);
            if (!extractResult.Success || extractResult.Value == null)
                return Result<CreateNodeResponse>.Fail(extractResult.Error!);

            var extracted = extractResult.Value;
            var newNode = new NoteNode
            {
                Id = Guid.NewGuid(),
                Title = extracted.Title,
                Note = extracted.Note,
                Tags = extracted.Tags,
                Relationships = extracted.Relationships
            };

            try
            {
                await _noteNodeRepository.SaveAsync(noteGraphId, newNode);
                return Result<CreateNodeResponse>.Ok(new CreateNodeResponse { Id = newNode.Id, Title = newNode.Title });
            }
            catch
            {
                return Result<CreateNodeResponse>.Fail("Failed to save extracted node.");
            }
        }

        public async Task<Result<SearchNodesResponse>> SearchNodesByContent(Guid noteGraphId, string query, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return Result<SearchNodesResponse>.Fail(metadataResult.Error!);

            var nodes = await _noteNodeRepository.SearchAsync(noteGraphId, query, ct);
            var results = nodes.Select(n =>
            {
                var matchedTitle = n.Title.Contains(query, StringComparison.OrdinalIgnoreCase);
                var matchedNote = n.Note.Contains(query, StringComparison.OrdinalIgnoreCase);
                var snippet = matchedNote ? BuildSnippet(n.Note, query) : n.Title;
                return new NodeSearchResult
                {
                    Id = n.Id,
                    Title = n.Title,
                    Snippet = snippet,
                    MatchedTitle = matchedTitle,
                    MatchedNote = matchedNote
                };
            }).ToList();

            return Result<SearchNodesResponse>.Ok(new SearchNodesResponse { Results = results });
        }

        public async Task<Result<GetPinnedNodesResponse>> GetPinnedNodes(Guid noteGraphId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return Result<GetPinnedNodesResponse>.Fail(metadataResult.Error!);

            var nodes = await _noteNodeRepository.GetAllByGraphIdAsync(noteGraphId, ct);
            var pinned = nodes
                .Where(n => n.Metadata.IsPinned)
                .Select(n => new PinnedNodeResult { Id = n.Id, Title = n.Title })
                .ToList();

            return Result<GetPinnedNodesResponse>.Ok(new GetPinnedNodesResponse { Nodes = pinned });
        }

        public async Task<Result<SetPinnedResponse>> SetNodePinned(Guid noteGraphId, Guid noteNodeId, bool isPinned, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return Result<SetPinnedResponse>.Fail(metadataResult.Error!);

            var node = await _noteNodeRepository.GetByIdAsync(noteGraphId, noteNodeId, ct);
            if (node is null)
                return Result<SetPinnedResponse>.Fail("Node not found.");

            node.Metadata.IsPinned = isPinned;

            try
            {
                await _noteNodeRepository.SaveAsync(noteGraphId, node);
                return Result<SetPinnedResponse>.Ok(new SetPinnedResponse { NodeId = noteNodeId, IsPinned = isPinned });
            }
            catch
            {
                return Result<SetPinnedResponse>.Fail("Failed to update pin status.");
            }
        }

        private static string BuildSnippet(string text, string query, int halfWindow = 80)
        {
            var idx = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return text.Length <= halfWindow * 2 ? text : text[..(halfWindow * 2)] + "...";

            var start = Math.Max(0, idx - halfWindow);
            var end = Math.Min(text.Length, idx + query.Length + halfWindow);
            var snippet = text[start..end];
            if (start > 0) snippet = "..." + snippet;
            if (end < text.Length) snippet += "...";
            return snippet;
        }

        public async Task<Result<EditNodeMetadataResponse>> EditNodeMetadataByIds(EditNodeMetadataRequest editNodeMetadataRequest, Guid noteGraphId, Guid noteNodeId, CancellationToken ct)
        {
            var graphDataResult = await _noteGraphAccessService.GetAuthorizedFullDocumentAsync(noteGraphId, ct);
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
                await _noteNodeRepository.SaveAsync(noteGraphId, existingNode);
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
