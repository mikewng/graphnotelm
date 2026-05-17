using graphnotelm.Core.Models;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Core.Utils;
using graphnotelm.Core.Utils.Tools;
using Microsoft.Extensions.AI;

namespace graphnotelm.Core.Services
{
    public class GraphToolFactory
    {
        private readonly IGraphAnalysisService _graphAnalysis;

        public GraphToolFactory(IGraphAnalysisService graphAnalysis)
        {
            _graphAnalysis = graphAnalysis;
        }

        public IReadOnlyList<AIFunction> Build(NoteGraphDocument document, GraphView graph)
        {
            var content = new GraphContentTools(document);
            var analysis = new GraphAnalysisTools(document, _graphAnalysis);

            return
            [
                // Graph Content Functions
                AIFunctionFactory.Create(content.GetNodeById),

                // Graph Analysis Functions
                AIFunctionFactory.Create(analysis.FindWeakestPath)
            ];
        }
    }
}
