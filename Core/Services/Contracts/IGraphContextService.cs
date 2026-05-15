using graphnotelm.Core.Models;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface IGraphContextService
    {
        public Task<Result<GraphContext>> InferContextAsync(NoteGraphDocument graph);
    }
}
