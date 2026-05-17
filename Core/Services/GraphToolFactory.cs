using graphnotelm.Core.Models;
using graphnotelm.Core.Utils;
using graphnotelm.Core.Utils.Tools;
using Microsoft.Extensions.AI;

namespace graphnotelm.Core.Services
{
    public class GraphToolFactory
    {
        public static IReadOnlyList<AIFunction> Build(NoteGraphDocument document, GraphView graph)
        {
            var content = new GraphContentTools(document);
            var analysis = new GraphAnalysisTools(document);

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
