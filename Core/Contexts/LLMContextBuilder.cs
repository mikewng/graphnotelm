using graphnotelm.Core.Models;
using graphnotelm.Core.Utils;
using System.Text;
using System.Text.Json;

namespace graphnotelm.Core.Contexts
{
    public class LLMPrompt
    {
        public string System { get; set; } = "";
        public string User { get; set; } = "";
    }
    public class LLMContextBuilder
    {
        public LLMPrompt BuildNodeAnalysisPrompt(NoteGraphDocument document, GraphView graph, Guid targetNodeId)
        {
            var node = graph.GetNode(targetNodeId);
            var outgoing = graph.GetOutgoing(targetNodeId);
            var incoming = graph.GetIncoming(targetNodeId);
            var existingLLM = node.Metadata.GetRawNamespace("llm");

            var neighborContext = new StringBuilder();
            foreach (var edge in outgoing)
            {
                var target = graph.GetNode(edge.TargetNodeId);
                var relDef = edge.GetType;
                neighborContext.AppendLine(
                    $"- THIS NODE [{relDef}] → {target.Title}: " +
                    $"\"{target.Note}\"");
            }

            foreach (var edge in incoming)
            {
                var source = graph.GetNode(edge.TargetNodeId);
                var relDef = edge.GetType;
                neighborContext.AppendLine(
                    $"- {source.Title} [{relDef}] → THIS NODE: " +
                    $"\"{source.Note}\"");
            }

            var systemPrompt = $"""
                You are analyzing a node in a knowledge graph.
                
                Your job is to analyze this node in context of its 
                neighbors and produce structured metadata. Write your 
                analysis as JSON matching this schema hint:
                You have access to previously computed graph metrics 
                and your own prior analysis. Build on your prior 
                analysis — don't start from scratch each time.
            
                Respond ONLY with a JSON object for the "llm" namespace. 
                No markdown, no preamble.
                """;

            var userPrompt = $"""
                ## Target Node
                
                Title: {node.Title}
                Note: {node.Note}
                User Confidence: {node.Metadata.UserConfidenceRate}

                ## Relationships
                {neighborContext}

                ## Your Prior Analysis (update and improve this)
                {JsonSerializer.Serialize(existingLLM)}
                """;

            return new LLMPrompt()
            {
                System = systemPrompt,
                User = userPrompt,
            };
        }

        public LLMPrompt BuildGraphOverviewPrompt(NoteGraphDocument document, GraphView graphView)
        {

            return new LLMPrompt() { };
        }
    }
}
