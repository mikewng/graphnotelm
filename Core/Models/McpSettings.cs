namespace graphnotelm.Core.Models
{
    public class McpSettings
    {
        public string SecretKey { get; set; } = string.Empty;
        public Guid? LocalUserId { get; set; }
        public string UserFilePath { get; set; } = string.Empty;
    }
}
