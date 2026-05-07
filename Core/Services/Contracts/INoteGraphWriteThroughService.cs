using graphnotelm.Core.Models;

namespace graphnotelm.Core.Services.Contracts
{
    public interface INoteGraphWriteThroughService
    {
        public Task<bool> ReplaceAsync(NoteGraphDocumentREADONLY document, Guid noteGraphId);
    }
}
