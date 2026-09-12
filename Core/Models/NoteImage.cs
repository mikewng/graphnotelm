namespace graphnotelm.Core.Models
{
    public class NoteImage
    {
        public Guid Id { get; set; }
        public Guid GraphId { get; set; }
        public Guid NodeId { get; set; }

        // Display only — never used to build a storage path.
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }

        // Location relative to the image store root, assigned by the repository on save.
        public string StoragePath { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
