namespace graphnotelm.Core.Clients
{
    public interface ILLMClient
    {
        public Task<string> CompleteAsync(string systemPrompt, string userPrompt);
    }
}
