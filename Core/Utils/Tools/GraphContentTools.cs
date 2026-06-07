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

        [Description("Gets a single node's full content by its title. Returns the ID, title, note content, confidence score, and all relationships.")]
        public NodeResult? GetNodeByTitle(
            [Description("The title of the node.")]
            string nodeTitle)
        {
            var node = _document.Nodes.Values
                .FirstOrDefault(n => string.Equals(n.Title.Trim(), nodeTitle.Trim(), StringComparison.OrdinalIgnoreCase));
            if (node is null) return null;

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

        [Description("Gets a single node's full content by its ID. Returns the title, note content, confidence score, and all relationships.")]
        public NodeResult? GetNodeById(
            [Description("The ID of the node to retrieve. Must be a valid GUID string.")]
            string noteNodeId)
        {
            if (!Guid.TryParse(noteNodeId, out var id) || !_document.Nodes.TryGetValue(id, out var node))
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

        [Description("Gets the list of tags that are associated with the graph.")]
        public List<string> GetListOfTags()
        {
            return _document.Tags.Values.Select(t => t.Name).ToList();
        }

        [Description("Gets multiple nodes' full content by their titles in one call. Use this instead of calling GetNodeByTitle repeatedly when you need several nodes.")]
        public List<NodeResult?> GetNodesByTitles(
            [Description("Array of node titles to retrieve.")]
            string[] nodeTitles)
        {
            return nodeTitles.Select(t => GetNodeByTitle(t)).ToList();
        }

        [Description("Gets a list of nodes that are associated with the tag name")]
        public List<NodeResult?> GetNodeByTag(
            [Description("The ID of the node to retrieve.")]
            string tagName)
        {
            List<NodeResult?> resultList = new List<NodeResult?>();

            Guid tagGuid = _document.Tags.FirstOrDefault(t => t.Value.Name == tagName).Key;
            var nodes = _document.Nodes.ToList().Where(node => node.Value.Tags.Contains(tagGuid));
            foreach (var node in nodes)
            {
                resultList.Add(new NodeResult(
                    node.Key,
                    node.Value.Title,
                    node.Value.Note,
                    node.Value.Metadata.UserConfidenceRate,
                    new List<NodeRelationshipResult>()
                    ));
            }

            return resultList;
        }

    }
}
