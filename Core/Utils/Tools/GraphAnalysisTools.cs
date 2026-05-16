using graphnotelm.Core.Models;
using graphnotelm.Core.Services.Contracts;
using System.ComponentModel;

namespace graphnotelm.Core.Utils.Tools
{
    public class GraphAnalysisTools
    {
        private readonly IGraphAnalysisService _graphAnalysis;
        public GraphAnalysisTools(IGraphAnalysisService graphAnalysisService)
        {
            _graphAnalysis = graphAnalysisService;
        }

        [Description("Gets a single node's full content based off node ID. This includes the title, confidence score, note content, and relationships")]
        public NoteNode GetKnowledgeFrontier(
            [Description("The graph note ID based off of type Guid in C#.")]
            Guid graphNoteId,
            [Description("The node ID based off of type Guid in C#.")]
            Guid noteNodeId
            )
        {
            return new NoteNode();
        }
    }
}
