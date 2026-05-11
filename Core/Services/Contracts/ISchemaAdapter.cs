using graphnotelm.Core.Models;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface ISchemaAdapter
    {
        public Result<NoteGraphDocument> AdaptToNoteGraphDocument<T>();
    }
}
