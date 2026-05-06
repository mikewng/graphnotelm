using System.ComponentModel.DataAnnotations;

namespace graphnotelm.Core.Models.DTOs
{
    public class CreateGraphRequest
    {
        [Required]
        public string Name { get; set; }
        public bool isPublic { get; set; } = false;
        public bool isDeleted { get; set; } = false;
    }

    public class CreateNodeRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public Dictionary<Guid, string> Relationships { get; set; } = new Dictionary<Guid, string>();
        public List<string> Tags { get; set; } = new List<string>();
    }

    public class EditNodeRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public Dictionary<Guid, string> Relationships { get; set; } = new Dictionary<Guid, string>();
        public List<string> Tags { get; set; } = new List<string>();
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

}
