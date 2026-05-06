using System.ComponentModel.DataAnnotations;

namespace graphnotelm.Core.Models.DTOs
{
    public class CreateGraphResponse
    {
        public Guid Id { get; set; }
        public bool IsSuccess { get; set; }
    }

    public class GetGraphResponse
    {
        public Guid Id { get; set; }
        public Dictionary<Guid, TagDefinition> Tags { get; set; } = new();
        public Dictionary<Guid, RelationshipDefinition> Relationships { get; set; } = new();
        public Dictionary<Guid, NoteNode> Nodes { get; set; } = new();
    }

    public class GetGraphListResponse
    {
        public List<NoteGraphMetadata> GraphList { get; set; } = new List<NoteGraphMetadata>();
    }

    public class DeleteGraphResponse
    {
        public Guid id { get; set; }
        public bool isDeleted { get; set; }
    }

    public class CreateNodeResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
    }

    public class EditNodeResponse
    {
        public NoteNode NoteNodeContent { get; set; }
    }

    public class DeleteNodeResponse
    {
        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }
    }


    public class GetTagListResponse
    {
        public List<string> Tags { get; set; } = new();
    }

    public class CreateTagResponse
    {
        public string TagName { get; set; }
    }

    public class EditTagResponse
    {

    }

    public class DeleteTagResponse {
        public string TagName { get; set; }
    }

    public class GetRelationshipListResponse
    {
        public List<string> Relationships { get; set; } = new();

    }

    public class CreateRelationshipResponse
    {

    }

    public class EditRelationshipResponse
    {

    }

    public class DeleteRelationshipResponse
    {

    }
}
