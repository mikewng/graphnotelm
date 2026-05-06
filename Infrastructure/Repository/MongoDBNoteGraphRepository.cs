using graphnotelm.Core.Models;
using graphnotelm.Infrastructure.Repository.Contracts;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System.Text.Json;

namespace graphnotelm.Infrastructure.Repository
{
    public class MongoDBNoteGraphRepository : INoteGraphRepository
    {
        private readonly IMongoCollection<MongoDoc> _collection;

        public MongoDBNoteGraphRepository(IMongoClient client, IOptions<MongoDbSettings> settings)
        {
            var db = client.GetDatabase(settings.Value.DatabaseName);
            _collection = db.GetCollection<MongoDoc>(settings.Value.CollectionName);
        }

        public async Task<NoteGraphDocument?> GetByIdAsync(Guid noteGraphId, CancellationToken ct = default)
        {
            var result = await _collection
                .Find(d => d.Id == noteGraphId.ToString())
                .FirstOrDefaultAsync(ct);

            if (result is null)
                return null;

            return JsonSerializer.Deserialize<NoteGraphDocument>(result.Data);
        }

        public async Task SaveAsync(NoteGraphDocument document)
        {
            var doc = new MongoDoc(document.Id.ToString(), JsonSerializer.Serialize(document));

            await _collection.ReplaceOneAsync(
                d => d.Id == doc.Id,
                doc,
                new ReplaceOptions { IsUpsert = true }
            );
        }

        public async Task DeleteByIdAsync(Guid noteGraphId)
        {
            await _collection.DeleteOneAsync(d => d.Id == noteGraphId.ToString());
        }

        private record MongoDoc(
            [property: BsonId] string Id,
            string Data
        );
    }
}
