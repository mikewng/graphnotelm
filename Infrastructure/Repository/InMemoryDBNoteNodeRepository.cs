using graphnotelm.Core.Models;
using graphnotelm.Infrastructure.Repository.Contracts;
using System.Collections.Concurrent;

namespace graphnotelm.Infrastructure.Repository
{
    public class InMemoryDBNoteNodeRepository : INoteNodeRepository
    {
        private readonly ConcurrentDictionary<string, NoteNode> _store = new();
        public Task<NoteNode?> GetByIdAsync(Guid noteGraphId, Guid noteNodeId, CancellationToken ct = default)
        {
            string dictId = noteGraphId.ToString() + noteNodeId.ToString();
            _store.TryGetValue(dictId, out var node);
            return Task.FromResult(node);
        }

        public Task<List<NoteNode>> GetAllByGraphIdAsync(Guid noteGraphId, CancellationToken ct = default)
        {
            var prefix = noteGraphId.ToString();
            var nodes = _store
                .Where(kvp => kvp.Key.StartsWith(prefix))
                .Select(kvp => kvp.Value)
                .ToList();
            return Task.FromResult(nodes);
        }

        public Task<List<NoteNode>> SearchAsync(Guid noteGraphId, string query, CancellationToken ct = default)
        {
            var prefix = noteGraphId.ToString();
            var nodes = _store
                .Where(kvp => kvp.Key.StartsWith(prefix))
                .Select(kvp => kvp.Value)
                .Where(n => n.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || n.Note.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return Task.FromResult(nodes);
        }
        public Task SaveAsync(Guid noteGraphId, NoteNode node)
        {
            string dictId = noteGraphId.ToString() + node.Id.ToString();
            _store[dictId] = node;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid noteGraphId, Guid nodeId)
        {
            string dictId = noteGraphId.ToString() + nodeId.ToString();
            _store.TryRemove(dictId, out _);
            return Task.CompletedTask;
        }
    }
}
