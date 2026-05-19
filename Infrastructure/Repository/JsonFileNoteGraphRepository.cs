using graphnotelm.Core.Models;
using graphnotelm.Infrastructure.Repository.Contracts;
using System.Collections.Concurrent;
using System.Text.Json;

namespace graphnotelm.Infrastructure.Repository
{
    public class JsonFileNoteGraphRepository : INoteGraphRepository
    {
        private readonly ConcurrentDictionary<Guid, NoteGraphDocument> _store = new();
        private readonly string _filePath;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public JsonFileNoteGraphRepository(IConfiguration configuration)
        {
            _filePath = configuration["NoteGraphStore:FilePath"] ?? "/app/data/notegraphs.json";
            Load();
        }

        private void Load()
        {
            if (!File.Exists(_filePath)) return;
            try
            {
                var json = File.ReadAllText(_filePath);
                var data = JsonSerializer.Deserialize<Dictionary<Guid, NoteGraphDocument>>(json);
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

        public Task<NoteGraphDocument?> GetByIdAsync(Guid noteGraphId, CancellationToken ct = default)
        {
            _store.TryGetValue(noteGraphId, out var document);
            return Task.FromResult(document);
        }

        public async Task SaveAsync(NoteGraphDocument document)
        {
            _store[document.Id] = new NoteGraphDocument
            {
                Id = document.Id,
                UserId = document.UserId,
                Context = document.Context,
                Tags = document.Tags,
                Relationships = document.Relationships
            };
            await PersistAsync();
        }

        public async Task DeleteByIdAsync(Guid noteGraphId)
        {
            _store.TryRemove(noteGraphId, out _);
            await PersistAsync();
        }
    }
}
