using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using graphnotelm.Core.Models;
using graphnotelm.Infrastructure.Repository.Contracts;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace graphnotelm.Infrastructure.Repository
{
    public class DynamoDBNoteGraphRepository : INoteGraphRepository
    {
        private readonly IAmazonDynamoDB _client;
        private readonly string _tableName;

        public DynamoDBNoteGraphRepository(IAmazonDynamoDB client, IOptions<DynamoDbSettings> settings)
        {
            _client = client;
            _tableName = settings.Value.TableName;
        }

        public async Task<NoteGraphDocument?> GetByIdAsync(Guid noteGraphId, CancellationToken ct = default)
        {
            var response = await _client.GetItemAsync(new GetItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["Id"] = new AttributeValue { S = noteGraphId.ToString() }
                }
            }, ct);

            if (response.Item == null || response.Item.Count == 0)
                return null;

            return JsonSerializer.Deserialize<NoteGraphDocument>(response.Item["Data"].S);
        }

        public async Task SaveAsync(NoteGraphDocument document)
        {
            await _client.PutItemAsync(new PutItemRequest
            {
                TableName = _tableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["Id"] = new AttributeValue { S = document.Id.ToString() },
                    ["Data"] = new AttributeValue { S = JsonSerializer.Serialize(document) }
                }
            });
        }

        public async Task DeleteByIdAsync(Guid noteGraphId)
        {
            await _client.DeleteItemAsync(new DeleteItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["Id"] = new AttributeValue { S = noteGraphId.ToString() }
                }
            });
        }
    }
}
