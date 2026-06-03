using graphnotelm.Core.Models;
using Microsoft.Extensions.AI;

namespace graphnotelm.Core.Services.Contracts
{
    public interface ILLMProviderFactory
    {
        IChatClient GetClient(LLMProviderConfig config);
    }
}
