using graphnotelm.Core.Models;
using graphnotelm.Infrastructure.Repository.Contracts;
using System.Collections.Concurrent;

namespace graphnotelm.Infrastructure.Repository
{
    public class InMemoryDBNoteGraphRepository : INoteGraphRepository
    {
        private readonly ConcurrentDictionary<Guid, NoteGraphDocument> _store = new();

        public Task<NoteGraphDocument?> GetByIdAsync(Guid noteGraphId, CancellationToken ct = default)
        {
            _store.TryGetValue(noteGraphId, out var document);
            return Task.FromResult(document);
        }

        public Task SaveAsync(NoteGraphDocument document)
        {
            _store[document.Id] = document;
            return Task.CompletedTask;
        }

        public Task DeleteByIdAsync(Guid noteGraphId)
        {
            _store.TryRemove(noteGraphId, out _);
            return Task.CompletedTask;
        }
    }
}
