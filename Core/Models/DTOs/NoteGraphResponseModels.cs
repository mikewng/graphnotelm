using System.ComponentModel.DataAnnotations;

namespace graphnotelm.Core.Models.DTOs
{
    public class CreateGraphResponse
    {
        public Guid id { get; set; }
        public bool isSuccess { get; set; }
    }

    public class GetGraphResponse
    {
        NoteGraphDocumentREADONLY? noteGraphDocument { get; set; }
    }

    public class GetGraphListResponse
    {
        List<NoteGraphMetadata> graphList { get; set; } = new List<NoteGraphMetadata>();
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
        public NoteNode noteNodeContent { get; set; }
    }

    public class DeleteNodeResponse
    {
        public Guid id { get; set; }
        public bool isDeleted { get; set; }
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
