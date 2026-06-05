using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using graphnotelm.Core.Models;
using graphnotelm.Infrastructure.Repository.Contracts;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace graphnotelm.Infrastructure.Repository
{
    public class DynamoDBNoteNodeRepository : INoteNodeRepository
    {
        private readonly IAmazonDynamoDB _client;
        private readonly string _tableName;

        public DynamoDBNoteNodeRepository(IAmazonDynamoDB client, IOptions<DynamoDbNodeSettings> settings)
        {
            _client = client;
            _tableName = settings.Value.TableName;
        }

        public async Task<NoteNode?> GetByIdAsync(Guid noteGraphId, Guid noteNodeId, CancellationToken ct = default)
        {
            var response = await _client.GetItemAsync(new GetItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["GraphId"] = new AttributeValue { S = noteGraphId.ToString() },
                    ["NodeId"] = new AttributeValue { S = noteNodeId.ToString() }
                }
            }, ct);

            if (response.Item == null || response.Item.Count == 0)
                return null;

            return JsonSerializer.Deserialize<NoteNode>(response.Item["Data"].S);
        }

        public async Task<List<NoteNode>> GetAllByGraphIdAsync(Guid noteGraphId, CancellationToken ct = default)
        {
            var nodes = new List<NoteNode>();
            Dictionary<string, AttributeValue>? lastKey = null;

            do
            {
                var request = new QueryRequest
                {
                    TableName = _tableName,
                    KeyConditionExpression = "GraphId = :graphId",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":graphId"] = new AttributeValue { S = noteGraphId.ToString() }
                    },
                    ExclusiveStartKey = lastKey
                };

                var response = await _client.QueryAsync(request, ct);

                nodes.AddRange(response.Items
                    .Select(item => JsonSerializer.Deserialize<NoteNode>(item["Data"].S)!));

                lastKey = response.LastEvaluatedKey?.Count > 0 ? response.LastEvaluatedKey : null;
            }
            while (lastKey != null);

            return nodes;
        }

        public async Task<List<NoteNode>> SearchAsync(Guid noteGraphId, string query, CancellationToken ct = default)
        {
            // DynamoDB stores Title/Note inside the Data JSON blob so FilterExpression
            // cannot reach them. Fetch all nodes for the graph and filter in memory.
            var all = await GetAllByGraphIdAsync(noteGraphId, ct);
            return all
                .Where(n => n.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || n.Note.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public async Task SaveAsync(Guid noteGraphId, NoteNode node)
        {
            await _client.PutItemAsync(new PutItemRequest
            {
                TableName = _tableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["GraphId"] = new AttributeValue { S = noteGraphId.ToString() },
                    ["NodeId"] = new AttributeValue { S = node.Id.ToString() },
                    ["Data"] = new AttributeValue { S = JsonSerializer.Serialize(node) }
                }
            });
        }

        public async Task DeleteAsync(Guid noteGraphId, Guid nodeId)
        {
            await _client.DeleteItemAsync(new DeleteItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["GraphId"] = new AttributeValue { S = noteGraphId.ToString() },
                    ["NodeId"] = new AttributeValue { S = nodeId.ToString() }
                }
            });
        }
    }
}
