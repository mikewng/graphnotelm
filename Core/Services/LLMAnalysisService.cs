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
        private static readonly string[] TagColors =
        [
            "#6366F1", "#8B5CF6", "#EC4899", "#14B8A6",
            "#F59E0B", "#10B981", "#F97316", "#06B6D4"
        ];

        private static readonly string[] RelationshipColors =
        [
            "#EF4444", "#F97316", "#EAB308", "#22C55E",
            "#3B82F6", "#A855F7", "#EC4899", "#64748B"
        ];
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

        public async Task<Result<NoteGraphDocumentREADONLY>> ExtractGraphFromTextAsync(string? name, string content, CancellationToken ct)
        {
            // --- Pass 1: extract nodes, tags, and relationship type definitions ---
            var pass1Prompt = _contextBuilder.BuildGraphExtractionPass1Prompt(content);
            var pass1Messages = new List<ChatMessage>
            {
                new(ChatRole.System, pass1Prompt.System),
                new(ChatRole.User, pass1Prompt.User),
            };

            ChatResponse pass1Response;
            try
            {
                pass1Response = await _chatClient.GetResponseAsync(pass1Messages, cancellationToken: ct);
            }
            catch (HttpRequestException ex)
            {
                return Result<NoteGraphDocumentREADONLY>.Fail($"AI provider error (pass 1): {ex.Message}");
            }

            var pass1Raw = pass1Response.Messages.LastOrDefault()?.Text ?? "";
            var pass1Clean = pass1Raw.Replace("```json", "").Replace("```", "").Trim();

            JsonElement pass1Root;
            try
            {
                pass1Root = JsonDocument.Parse(pass1Clean).RootElement;
            }
            catch (JsonException)
            {
                return Result<NoteGraphDocumentREADONLY>.Fail("LLM returned invalid JSON on pass 1.");
            }

            var graphName = name;
            if (string.IsNullOrWhiteSpace(graphName)
                && pass1Root.TryGetProperty("graphName", out var gn)
                && gn.ValueKind == JsonValueKind.String)
            {
                graphName = gn.GetString();
            }
            if (string.IsNullOrWhiteSpace(graphName))
                graphName = "Untitled Graph";

            var tags = new Dictionary<Guid, TagDefinition>();
            var tagNameToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            if (pass1Root.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
            {
                var tagIndex = 0;
                foreach (var el in tagsEl.EnumerateArray())
                {
                    var tagName = el.TryGetProperty("name", out var tn) ? tn.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(tagName))
                    {
                        var tagId = Guid.NewGuid();
                        tags[tagId] = new TagDefinition { Name = tagName!, Color = TagColors[tagIndex % TagColors.Length] };
                        tagNameToId[tagName!] = tagId;
                        tagIndex++;
                    }
                }
            }

            var relationshipTypes = new Dictionary<Guid, RelationshipDefinition>();
            if (pass1Root.TryGetProperty("relationshipTypes", out var relTypesEl) && relTypesEl.ValueKind == JsonValueKind.Array)
            {
                var relIndex = 0;
                foreach (var el in relTypesEl.EnumerateArray())
                {
                    var relName = el.TryGetProperty("name", out var rn) ? rn.GetString() : null;
                    var relInverse = el.TryGetProperty("inverse", out var ri) ? ri.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(relName))
                    {
                        relationshipTypes[Guid.NewGuid()] = new RelationshipDefinition { Name = relName!, Inverse = relInverse, Color = RelationshipColors[relIndex % RelationshipColors.Length] };
                        relIndex++;
                    }
                }
            }

            var alignedNodes = new Dictionary<Guid, NoteNode>();
            if (pass1Root.TryGetProperty("nodes", out var nodesEl) && nodesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in nodesEl.EnumerateArray())
                {
                    var nodeTitle = el.TryGetProperty("title", out var nt) ? nt.GetString() : null;
                    var nodeNote = el.TryGetProperty("note", out var nn) ? nn.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(nodeTitle)) continue;

                    var nodeTags = new List<Guid>();
                    if (el.TryGetProperty("tags", out var nodeTagsEl) && nodeTagsEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var tagEl in nodeTagsEl.EnumerateArray())
                        {
                            var tagName = tagEl.GetString();
                            if (tagName != null && tagNameToId.TryGetValue(tagName, out var tagId))
                                nodeTags.Add(tagId);
                        }
                    }

                    var nodeId = Guid.NewGuid();
                    alignedNodes[nodeId] = new NoteNode { Id = nodeId, Title = nodeTitle!, Note = nodeNote, Tags = nodeTags };
                }
            }

            if (alignedNodes.Count == 0)
                return Result<NoteGraphDocumentREADONLY>.Fail("LLM did not extract any nodes from the document.");

            // --- Pass 2: wire relationships between nodes ---
            var pass2Prompt = _contextBuilder.BuildGraphExtractionPass2Prompt(content, alignedNodes, relationshipTypes);
            var pass2Messages = new List<ChatMessage>
            {
                new(ChatRole.System, pass2Prompt.System),
                new(ChatRole.User, pass2Prompt.User),
            };

            ChatResponse pass2Response;
            try
            {
                pass2Response = await _chatClient.GetResponseAsync(pass2Messages, cancellationToken: ct);
            }
            catch (HttpRequestException ex)
            {
                return Result<NoteGraphDocumentREADONLY>.Fail($"AI provider error (pass 2): {ex.Message}");
            }

            var pass2Raw = pass2Response.Messages.LastOrDefault()?.Text ?? "";
            var pass2Clean = pass2Raw.Replace("```json", "").Replace("```", "").Trim();

            try
            {
                var pass2Root = JsonDocument.Parse(pass2Clean).RootElement;
                if (pass2Root.TryGetProperty("relationships", out var relsEl) && relsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in relsEl.EnumerateArray())
                    {
                        var sourceOk = el.TryGetProperty("sourceNodeId", out var srcEl)
                            && Guid.TryParse(srcEl.GetString(), out var sourceId)
                            && alignedNodes.ContainsKey(sourceId);

                        var targetOk = el.TryGetProperty("targetNodeId", out var tgtEl)
                            && Guid.TryParse(tgtEl.GetString(), out var targetId)
                            && alignedNodes.ContainsKey(targetId);

                        var relOk = el.TryGetProperty("relationshipId", out var relEl)
                            && Guid.TryParse(relEl.GetString(), out var relId)
                            && relationshipTypes.ContainsKey(relId);

                        if (sourceOk && targetOk && relOk)
                        {
                            Guid.TryParse(el.GetProperty("sourceNodeId").GetString(), out var validSource);
                            Guid.TryParse(el.GetProperty("targetNodeId").GetString(), out var validTarget);
                            Guid.TryParse(el.GetProperty("relationshipId").GetString(), out var validRel);

                            if (validSource != validTarget)
                                alignedNodes[validSource].Relationships.Add(new NodeRelationship { TargetNodeId = validTarget, RelationshipId = validRel });
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Pass 2 failure is non-fatal — return the graph without relationships
            }

            return Result<NoteGraphDocumentREADONLY>.Ok(new NoteGraphDocumentREADONLY
            {
                Name = graphName,
                Tags = tags,
                Relationships = relationshipTypes,
                Nodes = alignedNodes
            });
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
