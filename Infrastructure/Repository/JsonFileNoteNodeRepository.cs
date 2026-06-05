using graphnotelm.Core.Models;
using graphnotelm.Infrastructure.Repository.Contracts;
using System.Collections.Concurrent;
using System.Text.Json;

namespace graphnotelm.Infrastructure.Repository
{
    public class JsonFileNoteNodeRepository : INoteNodeRepository
    {
        private readonly ConcurrentDictionary<string, NoteNode> _store = new();
        private readonly string _filePath;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public JsonFileNoteNodeRepository(IConfiguration configuration)
        {
            _filePath = configuration["NoteNodeStore:FilePath"] ?? "/app/data/notenodes.json";
            Load();
        }

        private void Load()
        {
            if (!File.Exists(_filePath)) return;
            try
            {
                var json = File.ReadAllText(_filePath);
                var data = JsonSerializer.Deserialize<Dictionary<string, NoteNode>>(json);
                if (data == null) return;
                foreach (var (key, value) in data)
                    _store[key] = value;
            }
            catch (JsonException)
            {
                // Corrupted file — start with empty store; existing data remains on disk untouched
            }
        }

        private async Task PersistAsync()
        {
            await _writeLock.WaitAsync();
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (dir is not null) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(_store);
                await File.WriteAllTextAsync(_filePath, json);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private static string Key(Guid noteGraphId, Guid noteNodeId) =>
            noteGraphId.ToString() + noteNodeId.ToString();

        public Task<NoteNode?> GetByIdAsync(Guid noteGraphId, Guid noteNodeId, CancellationToken ct = default)
        {
            _store.TryGetValue(Key(noteGraphId, noteNodeId), out var node);
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

        public async Task SaveAsync(Guid noteGraphId, NoteNode node)
        {
            _store[Key(noteGraphId, node.Id)] = node;
            await PersistAsync();
        }

        public async Task DeleteAsync(Guid noteGraphId, Guid nodeId)
        {
            _store.TryRemove(Key(noteGraphId, nodeId), out _);
            await PersistAsync();
        }
    }
}
