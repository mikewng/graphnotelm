using System.ComponentModel.DataAnnotations;
using graphnotelm.Core.Models;

namespace graphnotelm.Core.Models.DTOs
{
    public class CreateGraphRequest
    {
        [Required]
        public string Name { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool isPublic { get; set; } = false;
        public bool isDeleted { get; set; } = false;
    }

    public class EditGraphMetadataRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class CreateNodeRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public NoteNodeMetadata Metadata { get; set; } = new();
        public List<NodeRelationship> Relationships { get; set; } = new();
        public List<Guid> Tags { get; set; } = new();
    }

    public class EditNodeRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public List<NodeRelationship> Relationships { get; set; } = new();
        public List<Guid> Tags { get; set; } = new();
    }

    public class CreateTagRequest
    {
        public string TagName { get; set; }
        public string TagColor { get; set; }
    }

    public class EditTagRequest
    {
        public Guid Id { get; set; }
        public string TagName { get; set; }
        public string TagColor { get; set; }
    }

    public class CreateRelationshipRequest
    {
        public string Type { get; set; }
        public string Color { get; set; }
        public string Inverse { get; set; }
    }

    public class EditRelationshipRequest
    {
        public Guid Id { get; set; }
        public string Type { get; set; }
        public string Color { get; set; }
        public string Inverse { get; set; }
    }
    public class SaveNodeContentRequest {
        public string? Title { get; set; }
        public string? Note { get; set; }
    }

    public class EditNodeMetadataRequest
    {
        public float? UserConfidenceRate { get; set; }
        public string? LLMMetadata { get; set; }
    }

    public class AddNodeTagRequest
    {
        [Required]
        public Guid TagId { get; set; }
    }

    public class AddNodeRelationshipRequest
    {
        [Required]
        public Guid TargetNodeId { get; set; }
        [Required]
        public Guid RelationshipId { get; set; }
    }
}
