namespace graphnotelm.Core.Models
{
    public class McpSettings
    {
        public string SecretKey { get; set; } = string.Empty;
        public Guid? LocalUserId { get; set; }
        public string UserFilePath { get; set; } = string.Empty;
        public string KeyFilePath { get; set; } = string.Empty;
        public string EnabledFilePath { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public int Port { get; set; } = 5240;
    }
}
