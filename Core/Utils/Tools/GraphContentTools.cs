using graphnotelm.Core.Models;
using System.ComponentModel;

namespace graphnotelm.Core.Utils.Tools
{
    public record NodeResult(
        Guid Id,
        string Title,
        string Note,
        float ConfidenceScore,
        IReadOnlyList<NodeRelationshipResult> Relationships
    );

    public record NodeRelationshipResult(
        Guid TargetNodeId,
        string TargetNodeTitle,
        string RelationshipType
    );

    public class GraphContentTools
    {
        private readonly NoteGraphDocument _document;

        public GraphContentTools(NoteGraphDocument document)
        {
            _document = document;
        }

        [Description("Gets a single node's full content by its ID. Returns the title, note content, confidence score, and all relationships.")]
        public NodeResult? GetNodeById(
            [Description("The ID of the node to retrieve.")]
            Guid noteNodeId)
        {
            if (!_document.Nodes.TryGetValue(noteNodeId, out var node))
                return null;

            var relationships = node.Relationships.Select(r =>
            {
                var typeName = _document.Relationships.TryGetValue(r.RelationshipId, out var rel)
                    ? rel.Name : "relates to";
                var targetTitle = _document.Nodes.TryGetValue(r.TargetNodeId, out var target)
                    ? target.Title : "unknown";
                return new NodeRelationshipResult(r.TargetNodeId, targetTitle, typeName);
            }).ToList();

            return new NodeResult(
                node.Id,
                node.Title,
                node.Note,
                node.Metadata.UserConfidenceRate,
                relationships
            );
        }
    }
}
