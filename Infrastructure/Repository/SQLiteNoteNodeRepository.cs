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

        // WAL lets readers proceed during writes and makes small frequent writes
        // (autosave) far cheaper than the default rollback journal. journal_mode is
        // persistent — set once here it sticks to the database file, covering the
        // EF Core connections that share it as well.
        private void EnsureTable()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            using var walCmd = conn.CreateCommand();
            walCmd.CommandText = "PRAGMA journal_mode=WAL;";
            walCmd.ExecuteScalar();

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

            // Additive migration: FolderId is a nullable column, so it can be added
            // in place without rebuilding the table. Existing rows become NULL (unfiled).
            if (columns.Count > 0 && !columns.Contains("FolderId"))
                AddFolderIdColumn(conn);
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
                    FolderId TEXT NULL,
                    Metadata TEXT NOT NULL DEFAULT '{}',
                    PRIMARY KEY (GraphId, NodeId)
                );
                """;
            cmd.ExecuteNonQuery();
        }

        private static void AddFolderIdColumn(SqliteConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "ALTER TABLE NoteNodes ADD COLUMN FolderId TEXT NULL;";
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

        private static NoteNode Hydrate(Guid nodeId, string title, string note, bool isPinned, Guid? folderId, string blobJson)
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
                Tags = blob.Tags,
                FolderId = folderId
            };
        }

        private static Guid? ReadFolderId(SqliteDataReader reader, int ordinal)
            => reader.IsDBNull(ordinal) ? null : Guid.Parse(reader.GetString(ordinal));

        // synchronous/busy_timeout are per-connection, so they run on every open.
        // NORMAL is durable-enough under WAL and skips an fsync per transaction.
        private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct = default)
        {
            var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var pragma = conn.CreateCommand();
            pragma.CommandText = "PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;";
            await pragma.ExecuteNonQueryAsync(ct);
            return conn;
        }

        public async Task<NoteNode?> GetByIdAsync(Guid noteGraphId, Guid noteNodeId, CancellationToken ct = default)
        {
            await using var conn = await OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT NodeId, Title, Note, IsPinned, FolderId, Metadata FROM NoteNodes WHERE GraphId = $graphId AND NodeId = $nodeId";
            cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
            cmd.Parameters.AddWithValue("$nodeId", noteNodeId.ToString());
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            return Hydrate(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3) != 0,
                ReadFolderId(reader, 4),
                reader.GetString(5));
        }

        public async Task<List<NoteNode>> GetAllByGraphIdAsync(Guid noteGraphId, CancellationToken ct = default)
        {
            await using var conn = await OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT NodeId, Title, Note, IsPinned, FolderId, Metadata FROM NoteNodes WHERE GraphId = $graphId";
            cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var nodes = new List<NoteNode>();
            while (await reader.ReadAsync(ct))
                nodes.Add(Hydrate(
                    Guid.Parse(reader.GetString(0)),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3) != 0,
                    ReadFolderId(reader, 4),
                    reader.GetString(5)));
            return nodes;
        }

        // Skeleton read: skips the Note column, which is the bulk of the row for
        // real notes. Relationships/tags still hydrate from the Metadata blob.
        public async Task<List<NoteNode>> GetAllSkeletonsByGraphIdAsync(Guid noteGraphId, CancellationToken ct = default)
        {
            await using var conn = await OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT NodeId, Title, IsPinned, FolderId, Metadata FROM NoteNodes WHERE GraphId = $graphId";
            cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var nodes = new List<NoteNode>();
            while (await reader.ReadAsync(ct))
                nodes.Add(Hydrate(
                    Guid.Parse(reader.GetString(0)),
                    reader.GetString(1),
                    string.Empty,
                    reader.GetInt32(2) != 0,
                    ReadFolderId(reader, 3),
                    reader.GetString(4)));
            return nodes;
        }

        public async Task<bool> ExistsAsync(Guid noteGraphId, Guid noteNodeId, CancellationToken ct = default)
        {
            await using var conn = await OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM NoteNodes WHERE GraphId = $graphId AND NodeId = $nodeId LIMIT 1";
            cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
            cmd.Parameters.AddWithValue("$nodeId", noteNodeId.ToString());
            return await cmd.ExecuteScalarAsync(ct) is not null;
        }

        // LIKE on the Metadata blob narrows candidates in SQL (GUIDs serialize as
        // lowercase "d" format); the exact check below removes any false positives.
        public async Task<List<NoteNode>> GetNodesReferencingIdAsync(Guid noteGraphId, Guid referencedId, CancellationToken ct = default)
        {
            await using var conn = await OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT NodeId, Title, Note, IsPinned, FolderId, Metadata FROM NoteNodes
                WHERE GraphId = $graphId AND Metadata LIKE $pattern
                """;
            cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
            cmd.Parameters.AddWithValue("$pattern", $"%{referencedId:D}%");
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var nodes = new List<NoteNode>();
            while (await reader.ReadAsync(ct))
            {
                var node = Hydrate(
                    Guid.Parse(reader.GetString(0)),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3) != 0,
                    ReadFolderId(reader, 4),
                    reader.GetString(5));
                if (node.Tags.Contains(referencedId)
                    || node.Relationships.Any(r => r.TargetNodeId == referencedId || r.RelationshipId == referencedId))
                    nodes.Add(node);
            }
            return nodes;
        }

        public async Task<List<NoteNode>> GetNodesByFolderAsync(Guid noteGraphId, Guid folderId, CancellationToken ct = default)
        {
            await using var conn = await OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT NodeId, Title, Note, IsPinned, FolderId, Metadata FROM NoteNodes WHERE GraphId = $graphId AND FolderId = $folderId";
            cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
            cmd.Parameters.AddWithValue("$folderId", folderId.ToString());
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var nodes = new List<NoteNode>();
            while (await reader.ReadAsync(ct))
                nodes.Add(Hydrate(
                    Guid.Parse(reader.GetString(0)),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3) != 0,
                    ReadFolderId(reader, 4),
                    reader.GetString(5)));
            return nodes;
        }

        public async Task<List<NoteNode>> SearchAsync(Guid noteGraphId, string query, CancellationToken ct = default)
        {
            await using var conn = await OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT NodeId, Title, Note, IsPinned, FolderId, Metadata FROM NoteNodes
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
                    ReadFolderId(reader, 4),
                    reader.GetString(5)));
            return nodes;
        }

        public async Task SaveAsync(Guid noteGraphId, NoteNode node)
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO NoteNodes (GraphId, NodeId, Title, Note, IsPinned, FolderId, Metadata)
                VALUES ($graphId, $nodeId, $title, $note, $isPinned, $folderId, $metadata)
                ON CONFLICT(GraphId, NodeId) DO UPDATE SET
                    Title    = excluded.Title,
                    Note     = excluded.Note,
                    IsPinned = excluded.IsPinned,
                    FolderId = excluded.FolderId,
                    Metadata = excluded.Metadata;
                """;
            cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
            cmd.Parameters.AddWithValue("$nodeId", node.Id.ToString());
            cmd.Parameters.AddWithValue("$title", node.Title);
            cmd.Parameters.AddWithValue("$note", node.Note);
            cmd.Parameters.AddWithValue("$isPinned", node.Metadata.IsPinned ? 1 : 0);
            cmd.Parameters.AddWithValue("$folderId", (object?)node.FolderId?.ToString() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$metadata", SerializeBlob(node));
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task SaveManyAsync(Guid noteGraphId, IEnumerable<NoteNode> nodes)
        {
            await using var conn = await OpenConnectionAsync();
            await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync();

            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO NoteNodes (GraphId, NodeId, Title, Note, IsPinned, FolderId, Metadata)
                VALUES ($graphId, $nodeId, $title, $note, $isPinned, $folderId, $metadata)
                ON CONFLICT(GraphId, NodeId) DO UPDATE SET
                    Title    = excluded.Title,
                    Note     = excluded.Note,
                    IsPinned = excluded.IsPinned,
                    FolderId = excluded.FolderId,
                    Metadata = excluded.Metadata;
                """;
            var graphIdParam = cmd.Parameters.Add("$graphId", SqliteType.Text);
            var nodeIdParam = cmd.Parameters.Add("$nodeId", SqliteType.Text);
            var titleParam = cmd.Parameters.Add("$title", SqliteType.Text);
            var noteParam = cmd.Parameters.Add("$note", SqliteType.Text);
            var isPinnedParam = cmd.Parameters.Add("$isPinned", SqliteType.Integer);
            var folderIdParam = cmd.Parameters.Add("$folderId", SqliteType.Text);
            var metadataParam = cmd.Parameters.Add("$metadata", SqliteType.Text);

            graphIdParam.Value = noteGraphId.ToString();
            foreach (var node in nodes)
            {
                nodeIdParam.Value = node.Id.ToString();
                titleParam.Value = node.Title;
                noteParam.Value = node.Note;
                isPinnedParam.Value = node.Metadata.IsPinned ? 1 : 0;
                folderIdParam.Value = (object?)node.FolderId?.ToString() ?? DBNull.Value;
                metadataParam.Value = SerializeBlob(node);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }

        public async Task DeleteAsync(Guid noteGraphId, Guid nodeId)
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM NoteNodes WHERE GraphId = $graphId AND NodeId = $nodeId";
            cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
            cmd.Parameters.AddWithValue("$nodeId", nodeId.ToString());
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
