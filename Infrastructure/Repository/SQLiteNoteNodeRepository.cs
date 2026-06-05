using graphnotelm.Core.Models;
using graphnotelm.Infrastructure.Repository.Contracts;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace graphnotelm.Infrastructure.Repository
{
    public class SQLiteNoteNodeRepository : INoteNodeRepository
    {
        private readonly string _connectionString;

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
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS NoteNodes (
                    GraphId TEXT NOT NULL,
                    NodeId  TEXT NOT NULL,
                    Data    TEXT NOT NULL,
                    PRIMARY KEY (GraphId, NodeId)
                );
                """;
            cmd.ExecuteNonQuery();
        }

        public async Task<NoteNode?> GetByIdAsync(Guid noteGraphId, Guid noteNodeId, CancellationToken ct = default)
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Data FROM NoteNodes WHERE GraphId = $graphId AND NodeId = $nodeId";
            cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
            cmd.Parameters.AddWithValue("$nodeId", noteNodeId.ToString());
            var result = await cmd.ExecuteScalarAsync(ct);
            if (result is not string json) return null;
            return JsonSerializer.Deserialize<NoteNode>(json);
        }

        public async Task<List<NoteNode>> GetAllByGraphIdAsync(Guid noteGraphId, CancellationToken ct = default)
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Data FROM NoteNodes WHERE GraphId = $graphId";
            cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var nodes = new List<NoteNode>();
            while (await reader.ReadAsync(ct))
            {
                var node = JsonSerializer.Deserialize<NoteNode>(reader.GetString(0));
                if (node != null) nodes.Add(node);
            }
            return nodes;
        }

        public async Task<List<NoteNode>> SearchAsync(Guid noteGraphId, string query, CancellationToken ct = default)
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT Data FROM NoteNodes
                WHERE GraphId = $graphId
                AND (
                    INSTR(LOWER(json_extract(Data, '$.Title')), LOWER($query)) > 0
                    OR INSTR(LOWER(json_extract(Data, '$.Note')),  LOWER($query)) > 0
                )
                """;
            cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
            cmd.Parameters.AddWithValue("$query", query);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var nodes = new List<NoteNode>();
            while (await reader.ReadAsync(ct))
            {
                var node = JsonSerializer.Deserialize<NoteNode>(reader.GetString(0));
                if (node != null) nodes.Add(node);
            }
            return nodes;
        }

        public async Task SaveAsync(Guid noteGraphId, NoteNode node)
        {
            var json = JsonSerializer.Serialize(node);
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO NoteNodes (GraphId, NodeId, Data)
                VALUES ($graphId, $nodeId, $data)
                ON CONFLICT(GraphId, NodeId) DO UPDATE SET Data = excluded.Data;
                """;
            cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
            cmd.Parameters.AddWithValue("$nodeId", node.Id.ToString());
            cmd.Parameters.AddWithValue("$data", json);
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
