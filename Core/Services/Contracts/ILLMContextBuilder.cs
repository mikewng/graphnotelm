using graphnotelm.Core.Models;
using graphnotelm.Core.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface ILLMContextBuilder
    {
        public LLMPrompt BuildNodeAnalysisPrompt(NoteGraphDocument document, GraphView graphView, Guid targetNodeId);
        public LLMPrompt BuildGraphOverviewPrompt(NoteGraphDocument document, GraphView graphView);
    }
}
