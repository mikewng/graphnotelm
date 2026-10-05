namespace graphnotelm.Core.Models
{
    public class NoteGraphDocumentREADONLY {
        public string Name { get; set; } = String.Empty;
        public Dictionary<Guid, TagDefinition> Tags { get; set; } = new();
        public Dictionary<Guid, FolderDefinition> Folders { get; set; } = new();
        public Dictionary<Guid, RelationshipDefinition> Relationships { get; set; } = new();
        public Dictionary<Guid, NoteNode> Nodes { get; set; } = new();
        // Flashcards live in their own table, so they travel alongside the nodes here.
        public List<FlashcardDefinition> Flashcards { get; set; } = new();
    }

    public class NoteGraphDocument
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public GraphContext Context { get; set; } = new();
        public Dictionary<Guid, TagDefinition> Tags { get; set; } = new();
        public Dictionary<Guid, FolderDefinition> Folders { get; set; } = new();
        public Dictionary<Guid, RelationshipDefinition> Relationships { get; set; } = new();
        public Dictionary<Guid, NoteNode> Nodes { get; set; } = new();
    }

    public class GraphContext
    {
        public string SystemPrompt { get; set; } = "";
        public string MetadataSchemaHint { get; set; } = "summary";
    }

    public class NoteNode
    {
        public Guid Id { get; set; }
        public NoteNodeMetadata Metadata { get; set; } = new();
        public string Title { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public List<NodeRelationship> Relationships { get; set; } = new();
        public List<Guid> Tags { get; set; } = new();

        // A node belongs to at most one folder within its graph. Null = unfiled.
        public Guid? FolderId { get; set; }
    }

    public class NoteNodeMetadata
    {
        // The user's self-rating. Once the note has been reviewed, measured confidence
        // (see MemoryModel.Confidence) takes over everywhere confidence is used.
        public float UserConfidenceRate { get; set; } = 0.0f;
        public string LLMMetadata { get; set; } = string.Empty;
        public bool IsPinned { get; set; } = false;
        // Null until the note's first review.
        public MemoryState? Memory { get; set; }
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

    public class FolderDefinition
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
