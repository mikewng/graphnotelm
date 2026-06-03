using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Core.Utils;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace graphnotelm.Core.Services
{
    public class LLMAnalysisService : ILLMAnalysisService
    {
        private readonly IChatClient _chatClient;
        private readonly ILLMContextBuilder _contextBuilder;
        private readonly IGraphAnalysisService _graphAnalysis;
        private readonly INoteGraphAccessService _noteGraphAccessService;
        private readonly INoteNodeRepository _noteNodeRepository;

        public LLMAnalysisService(
            IChatClient chatClient,
            ILLMContextBuilder contextBuilder,
            IGraphAnalysisService graphAnalysis,
            INoteGraphAccessService noteGraphAccess,
            INoteNodeRepository noteNodeRepository)
        {
            _chatClient = chatClient;
            _contextBuilder = contextBuilder;
            _graphAnalysis = graphAnalysis;
            _noteGraphAccessService = noteGraphAccess;
            _noteNodeRepository = noteNodeRepository;
        }

        public async Task<Result<EditNodeMetadataResponse>> AnalyzeNodeAsync(Guid noteGraphId, Guid nodeId, CancellationToken ct)
        {
            var graphDataResult = await _noteGraphAccessService.GetAuthorizedFullDocumentAsync(noteGraphId, ct);
            if (!graphDataResult.Success || graphDataResult.Value == null)
                return Result<EditNodeMetadataResponse>.Fail(graphDataResult.Error!);

            var document = graphDataResult.Value;
            if (!document.Nodes.TryGetValue(nodeId, out var node))
                return Result<EditNodeMetadataResponse>.Fail("Node not found.");

            var view = _graphAnalysis.BuildView(document, nodeId);
            var prompt = _contextBuilder.BuildNodeAnalysisPrompt(document, view, nodeId);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, prompt.System),
                new(ChatRole.User, prompt.User),
            };
            ChatResponse completion;
            try
            {
                completion = await _chatClient.GetResponseAsync(messages, cancellationToken: ct);
            }
            catch (HttpRequestException ex)
            {
                return Result<EditNodeMetadataResponse>.Fail($"AI provider error: {ex.Message}");
            }
            var response = completion.Messages.LastOrDefault()?.Text ?? "";

            var clean = response.Replace("```json", "").Replace("```", "").Trim();

            try
            {
                var root = JsonDocument.Parse(clean).RootElement;
                // Unwrap if the LLM wrapped its response in {"llm": {...}}
                var llmElement = root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty("llm", out var inner)
                    ? inner.Clone()
                    : root.Clone();
                node.Metadata.SetRawNamespace("llm", llmElement);
            }
            catch (JsonException)
            {
                return Result<EditNodeMetadataResponse>.Fail("LLM returned invalid JSON.");
            }

            try
            {
                await _noteNodeRepository.SaveAsync(noteGraphId, node);
            }
            catch
            {
                return Result<EditNodeMetadataResponse>.Fail("Failed to save updated metadata.");
            }

            return Result<EditNodeMetadataResponse>.Ok(new EditNodeMetadataResponse
            {
                NodeId = nodeId,
                Metadata = node.Metadata
            });
        }

        public Task<Result<EditNodeMetadataResponse>> AnalyzeNodeBatchAsync(Guid noteGraphId, List<Guid> noteNodeId)
        {
            throw new NotImplementedException();
        }

        public async Task<Result<CreateNodeRequest>> ExtractNodeFromPasteAsync(Guid noteGraphId, string pastedContent, CancellationToken ct)
        {
            var graphDataResult = await _noteGraphAccessService.GetAuthorizedFullDocumentAsync(noteGraphId, ct);
            if (!graphDataResult.Success || graphDataResult.Value == null)
                return Result<CreateNodeRequest>.Fail(graphDataResult.Error!);

            var document = graphDataResult.Value;
            var prompt = _contextBuilder.BuildNodeFromPastePrompt(document, pastedContent);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, prompt.System),
                new(ChatRole.User, prompt.User),
            };

            ChatResponse completion;
            try
            {
                completion = await _chatClient.GetResponseAsync(messages, cancellationToken: ct);
            }
            catch (HttpRequestException ex)
            {
                return Result<CreateNodeRequest>.Fail($"AI provider error: {ex.Message}");
            }

            var raw = completion.Messages.LastOrDefault()?.Text ?? "";
            var clean = raw.Replace("```json", "").Replace("```", "").Trim();

            JsonElement root;
            try
            {
                root = JsonDocument.Parse(clean).RootElement;
            }
            catch (JsonException)
            {
                return Result<CreateNodeRequest>.Fail("LLM returned invalid JSON.");
            }

            var title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            var note = root.TryGetProperty("note", out var n) ? n.GetString() ?? "" : "";

            if (string.IsNullOrWhiteSpace(title))
                return Result<CreateNodeRequest>.Fail("LLM did not return a valid title.");

            var tags = new List<Guid>();
            if (root.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in tagsEl.EnumerateArray())
                {
                    if (Guid.TryParse(el.GetString(), out var tagId) && document.Tags.ContainsKey(tagId))
                        tags.Add(tagId);
                }
            }

            var relationships = new List<NodeRelationship>();
            if (root.TryGetProperty("relationships", out var relsEl) && relsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in relsEl.EnumerateArray())
                {
                    var targetOk = el.TryGetProperty("targetNodeId", out var targetEl)
                        && Guid.TryParse(targetEl.GetString(), out var targetId)
                        && document.Nodes.ContainsKey(targetId);

                    var relOk = el.TryGetProperty("relationshipId", out var relEl)
                        && Guid.TryParse(relEl.GetString(), out var relId)
                        && document.Relationships.ContainsKey(relId);

                    if (targetOk && relOk)
                    {
                        Guid.TryParse(el.GetProperty("targetNodeId").GetString(), out var validTargetId);
                        Guid.TryParse(el.GetProperty("relationshipId").GetString(), out var validRelId);
                        relationships.Add(new NodeRelationship { TargetNodeId = validTargetId, RelationshipId = validRelId });
                    }
                }
            }

            return Result<CreateNodeRequest>.Ok(new CreateNodeRequest
            {
                Title = title,
                Note = note,
                Tags = tags,
                Relationships = relationships
            });
        }
    }
}
