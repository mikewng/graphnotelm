using graphnotelm.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface INoteGraphService
    {
        public Result<bool> GetNoteGraphById(Guid noteGraphId);
        public Result<bool> GetNote(Guid noteGraphId);

    }
}
