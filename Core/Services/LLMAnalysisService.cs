using graphnotelm.Core.Clients;
using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Core.Utils;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;
using System.Text.Json;

namespace graphnotelm.Core.Services
{
    public class LLMAnalysisService : ILLMAnalysisService
    {
        private readonly ILLMClient _llm;
        private readonly ILLMContextBuilder _contextBuilder;
        private readonly IGraphAnalysisService _graphAnalysis;
        private readonly INoteGraphAccessService _noteGraphAccessService;
        private readonly INoteGraphRepository _noteGraphRepository;

        public LLMAnalysisService(
            ILLMClient llm,
            ILLMContextBuilder contextBuilder,
            IGraphAnalysisService graphAnalysis,
            INoteGraphAccessService noteGraphAccess,
            INoteGraphRepository noteGraphRepository)
        {
            _llm = llm;
            _contextBuilder = contextBuilder;
            _graphAnalysis = graphAnalysis;
            _noteGraphAccessService = noteGraphAccess;
            _noteGraphRepository = noteGraphRepository;
        }

        public async Task<Result<EditNodeMetadataResponse>> AnalyzeNodeAsync(Guid noteGraphId, Guid nodeId, CancellationToken ct)
        {
            var metadataResult = await _noteGraphAccessService.GetAuthorizedMetadataAsync(noteGraphId, ct);
            if (!metadataResult.Success)
                return Result<EditNodeMetadataResponse>.Fail(metadataResult.Error!);

            var graphDataResult = await _noteGraphAccessService.GetAuthorizedGraphDataAsync(noteGraphId, ct);
            if (!graphDataResult.Success || graphDataResult.Value == null)
                return Result<EditNodeMetadataResponse>.Fail(graphDataResult.Error!);

            var document = graphDataResult.Value;
            if (!document.Nodes.TryGetValue(nodeId, out var node))
                return Result<EditNodeMetadataResponse>.Fail("Node not found.");

            var view = _graphAnalysis.BuildView(document, nodeId);
            var prompt = _contextBuilder.BuildNodeAnalysisPrompt(document, view, nodeId);

            var response = await _llm.CompleteAsync(prompt.System, prompt.User);

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
                await _noteGraphRepository.SaveAsync(document);
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
    }
}
