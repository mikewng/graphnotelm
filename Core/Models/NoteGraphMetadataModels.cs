namespace graphnotelm.Core.Models
{
    public class NoteGraphTag
    {
        public int Id { get; set; }
        public string Name { get; set; } = String.Empty;
    }
    public class NoteGraphMetadata
    {
        public Guid Id { get; set; }
        public Guid NoteGraphId { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; } = String.Empty;
        public List<NoteGraphTag> GraphTags { get; set; } = new List<NoteGraphTag>();
        public bool isPublic { get; set; } = false;
        public bool isDeleted { get; set; } = false;
    }
}
