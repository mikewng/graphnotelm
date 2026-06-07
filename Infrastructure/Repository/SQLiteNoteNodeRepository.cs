using graphnotelm.Core.Models;
using graphnotelm.Infrastructure.Repository.Contracts;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace graphnotelm.Infrastructure.Repository
{
    public class SQLiteNoteNodeRepository : INoteNodeRepository
    {
        private readonly string _connectionString;
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public SQLiteNoteNodeRepository(IConfiguration configuration)
        {
            var raw = configuration.GetConnectionString("LocalDB")
                ?? throw new InvalidOperationException("LocalDB connection string missing");
            _connectionString = Environment.ExpandEnvironmentVariables(raw);
            EnsureTable();
        }

        private void EnsureTable()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            using var pragmaCmd = conn.CreateCommand();
            pragmaCmd.CommandText = "PRAGMA table_info(NoteNodes)";
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var reader = pragmaCmd.ExecuteReader())
                while (reader.Read())
                    columns.Add(reader.GetString(1));

            if (columns.Count == 0)
                CreateSchema(conn);
            else if (columns.Contains("Data") && !columns.Contains("Title"))
                MigrateSchema(conn);
        }

        private static void CreateSchema(SqliteConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE NoteNodes (
                    GraphId  TEXT NOT NULL,
                    NodeId   TEXT NOT NULL,
                    Title    TEXT NOT NULL DEFAULT '',
                    Note     TEXT NOT NULL DEFAULT '',
                    IsPinned INTEGER NOT NULL DEFAULT 0,
                    Metadata TEXT NOT NULL DEFAULT '{}',
                    PRIMARY KEY (GraphId, NodeId)
                );
                """;
            cmd.ExecuteNonQuery();
        }

        private static void MigrateSchema(SqliteConnection conn)
        {
            using var tx = conn.BeginTransaction();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;

                cmd.CommandText = """
                    CREATE TABLE NoteNodes_new (
                        GraphId  TEXT NOT NULL,
                        NodeId   TEXT NOT NULL,
                        Title    TEXT NOT NULL DEFAULT '',
                        Note     TEXT NOT NULL DEFAULT '',
                        IsPinned INTEGER NOT NULL DEFAULT 0,
                        Metadata TEXT NOT NULL DEFAULT '{}',
                        PRIMARY KEY (GraphId, NodeId)
                    );
                    """;
                cmd.ExecuteNonQuery();

                cmd.CommandText = """
                    INSERT INTO NoteNodes_new (GraphId, NodeId, Title, Note, IsPinned, Metadata)
                    SELECT
                        GraphId,
                        NodeId,
                        COALESCE(json_extract(Data, '$.Title'), ''),
                        COALESCE(json_extract(Data, '$.Note'), ''),
                        COALESCE(json_extract(Data, '$.Metadata.IsPinned'), 0),
                        json_object(
                            'UserConfidenceRate', COALESCE(json_extract(Data, '$.Metadata.UserConfidenceRate'), 0.0),
                            'LLMMetadata',        COALESCE(json_extract(Data, '$.Metadata.LLMMetadata'), ''),
                            'Relationships',      json(COALESCE(json_extract(Data, '$.Relationships'), '[]')),
                            'Tags',               json(COALESCE(json_extract(Data, '$.Tags'), '[]'))
                        )
                    FROM NoteNodes;
                    """;
                cmd.ExecuteNonQuery();

                cmd.CommandText = "DROP TABLE NoteNodes;";
                cmd.ExecuteNonQuery();

                cmd.CommandText = "ALTER TABLE NoteNodes_new RENAME TO NoteNodes;";
                cmd.ExecuteNonQuery();

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private class NodeBlob
        {
            public float UserConfidenceRate { get; set; }
            public string LLMMetadata { get; set; } = string.Empty;
            public List<NodeRelationship> Relationships { get; set; } = new();
            public List<Guid> Tags { get; set; } = new();
        }

        private static string SerializeBlob(NoteNode node) =>
            JsonSerializer.Serialize(new NodeBlob
            {
                UserConfidenceRate = node.Metadata.UserConfidenceRate,
                LLMMetadata = node.Metadata.LLMMetadata,
                Relationships = node.Relationships,
                Tags = node.Tags
            });

        private static NoteNode Hydrate(Guid nodeId, string title, string note, bool isPinned, string blobJson)
        {
            var blob = JsonSerializer.Deserialize<NodeBlob>(blobJson, _jsonOptions) ?? new NodeBlob();
            return new NoteNode
            {
                Id = nodeId,
                Title = title,
                Note = note,
                Metadata = new NoteNodeMetadata
                {
                    UserConfidenceRate = blob.UserConfidenceRate,
                    LLMMetadata = blob.LLMMetadata,
                    IsPinned = isPinned
                },
                Relationships = blob.Relationships,
                Tags = blob.Tags
            };
        }

        public async Task<NoteNode?> GetByIdAsync(Guid noteGraphId, Guid noteNodeId, CancellationToken ct = default)
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT NodeId, Title, Note, IsPinned, Metadata FROM NoteNodes WHERE GraphId = $graphId AND NodeId = $nodeId";
            cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
            cmd.Parameters.AddWithValue("$nodeId", noteNodeId.ToString());
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            return Hydrate(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3) != 0,
                reader.GetString(4));
        }

        public async Task<List<NoteNode>> GetAllByGraphIdAsync(Guid noteGraphId, CancellationToken ct = default)
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT NodeId, Title, Note, IsPinned, Metadata FROM NoteNodes WHERE GraphId = $graphId";
            cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var nodes = new List<NoteNode>();
            while (await reader.ReadAsync(ct))
                nodes.Add(Hydrate(
                    Guid.Parse(reader.GetString(0)),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3) != 0,
                    reader.GetString(4)));
            return nodes;
        }

        public async Task<List<NoteNode>> SearchAsync(Guid noteGraphId, string query, CancellationToken ct = default)
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT NodeId, Title, Note, IsPinned, Metadata FROM NoteNodes
                WHERE GraphId = $graphId
                AND (INSTR(LOWER(Title), LOWER($query)) > 0 OR INSTR(LOWER(Note), LOWER($query)) > 0)
                """;
            cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
            cmd.Parameters.AddWithValue("$query", query);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var nodes = new List<NoteNode>();
            while (await reader.ReadAsync(ct))
                nodes.Add(Hydrate(
                    Guid.Parse(reader.GetString(0)),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3) != 0,
                    reader.GetString(4)));
            return nodes;
        }

        public async Task SaveAsync(Guid noteGraphId, NoteNode node)
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO NoteNodes (GraphId, NodeId, Title, Note, IsPinned, Metadata)
                VALUES ($graphId, $nodeId, $title, $note, $isPinned, $metadata)
                ON CONFLICT(GraphId, NodeId) DO UPDATE SET
                    Title    = excluded.Title,
                    Note     = excluded.Note,
                    IsPinned = excluded.IsPinned,
                    Metadata = excluded.Metadata;
                """;
            cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
            cmd.Parameters.AddWithValue("$nodeId", node.Id.ToString());
            cmd.Parameters.AddWithValue("$title", node.Title);
            cmd.Parameters.AddWithValue("$note", node.Note);
            cmd.Parameters.AddWithValue("$isPinned", node.Metadata.IsPinned ? 1 : 0);
            cmd.Parameters.AddWithValue("$metadata", SerializeBlob(node));
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteAsync(Guid noteGraphId, Guid nodeId)
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM NoteNodes WHERE GraphId = $graphId AND NodeId = $nodeId";
            cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
            cmd.Parameters.AddWithValue("$nodeId", nodeId.ToString());
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
