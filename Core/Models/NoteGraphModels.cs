namespace graphnotelm.Core.Models
{
    public class NoteGraphDocument
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Dictionary<string, TagDefinition> Tags { get; set; } = new();
        public Dictionary<string, RelationshipDefinition> Relationships { get; set; } = new();
        public Dictionary<Guid, NoteNode> Nodes { get; set; } = new();
    }

    public class NoteNode
    {
        public string Title { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public List<NodeRelationship> Relationships { get; set; } = new();
        public List<string> Tags { get; set; } = new();
    }

    public class NodeRelationship
    {
        public Guid TargetNodeId { get; set; }
        public string Type { get; set; } = string.Empty;
    }

    public class TagDefinition
    {
        public string Color { get; set; } = string.Empty;
    }

    public class RelationshipDefinition
    {
        public string Color { get; set; } = string.Empty;
        public string Inverse { get; set; } = string.Empty;
    }
}
