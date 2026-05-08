namespace graphnotelm.Core.Models
{
    public class NoteGraphDocumentREADONLY {
        public string Name { get; set; } = String.Empty;
        public Dictionary<Guid, TagDefinition> Tags { get; set; } = new();
        public Dictionary<Guid, RelationshipDefinition> Relationships { get; set; } = new();
        public Dictionary<Guid, NoteNode> Nodes { get; set; } = new();
    }

    public class NoteGraphDocument
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Dictionary<Guid, TagDefinition> Tags { get; set; } = new();
        public Dictionary<Guid, RelationshipDefinition> Relationships { get; set; } = new();
        public Dictionary<Guid, NoteNode> Nodes { get; set; } = new();
    }

    public class NoteNode
    {
        public Guid Id { get; set; }
        public NoteNodeMetadata Metadata { get; set; } = new();
        public string Title { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public List<NodeRelationship> Relationships { get; set; } = new();
        public List<Guid> Tags { get; set; } = new();
    }

    public class NoteNodeMetadata
    {
        public float LearningRate { get; set; } = 0.0f;
        public string LLMMetadata { get; set; } = string.Empty;
    }

    public class NodeRelationship
    {
        public Guid TargetNodeId { get; set; }
        public Guid RelationshipId { get; set; }
    }

    public class TagDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
    }

    public class RelationshipDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Inverse { get; set; } = string.Empty;
    }
}
