namespace graphnotelm.Core.Models
{
    public class LLMPrompt
    {
        public string System { get; set; } = "";
        public string User { get; set; } = "";
    }
    public class LLMChatMessage
    {
        public string Role { get; set; } = "system";
        public string Content { get; set; } = "";
    }
}
