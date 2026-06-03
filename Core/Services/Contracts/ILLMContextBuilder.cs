using graphnotelm.Core.Models;
using graphnotelm.Core.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface ILLMContextBuilder
    {
        public LLMPrompt BuildNodeAnalysisPrompt(NoteGraphDocument document, GraphView graphView, Guid targetNodeId);
        public LLMPrompt BuildGraphOverviewPrompt(NoteGraphDocument document, GraphView graphView);
        public LLMPrompt BuildNodeFromPastePrompt(NoteGraphDocument document, string pastedContent);
        public LLMPrompt BuildGraphExtractionPass1Prompt(string content);
        public LLMPrompt BuildGraphExtractionPass2Prompt(string content, Dictionary<Guid, NoteNode> nodes, Dictionary<Guid, RelationshipDefinition> relationshipTypes);
    }
}
