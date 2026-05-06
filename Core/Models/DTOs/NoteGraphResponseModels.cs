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
}
