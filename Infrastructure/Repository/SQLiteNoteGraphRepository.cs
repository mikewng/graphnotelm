using graphnotelm.Core.Models;
using graphnotelm.Infrastructure.Repository.Contracts;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace graphnotelm.Infrastructure.Repository
{
    public class SQLiteNoteGraphRepository : INoteGraphRepository
    {
        private readonly string _connectionString;

        public SQLiteNoteGraphRepository(IConfiguration configuration)
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
            using var walCmd = conn.CreateCommand();
            walCmd.CommandText = "PRAGMA journal_mode=WAL;";
            walCmd.ExecuteScalar();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS NoteGraphDocuments (
                    Id   TEXT NOT NULL PRIMARY KEY,
                    Data TEXT NOT NULL
                );
                """;
            cmd.ExecuteNonQuery();
        }

        private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct = default)
        {
            var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var pragma = conn.CreateCommand();
            pragma.CommandText = "PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;";
            await pragma.ExecuteNonQueryAsync(ct);
            return conn;
        }

        public async Task<NoteGraphDocument?> GetByIdAsync(Guid noteGraphId, CancellationToken ct = default)
        {
            await using var conn = await OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Data FROM NoteGraphDocuments WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", noteGraphId.ToString());
            var result = await cmd.ExecuteScalarAsync(ct);
            if (result is not string json) return null;
            return JsonSerializer.Deserialize<NoteGraphDocument>(json);
        }

        public async Task SaveAsync(NoteGraphDocument document)
        {
            // Nodes are stored separately in NoteNodes table
            var toStore = new NoteGraphDocument
            {
                Id = document.Id,
                UserId = document.UserId,
                Context = document.Context,
                Tags = document.Tags,
                Folders = document.Folders,
                Relationships = document.Relationships
            };
            var json = JsonSerializer.Serialize(toStore);

            await using var conn = await OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO NoteGraphDocuments (Id, Data)
                VALUES ($id, $data)
                ON CONFLICT(Id) DO UPDATE SET Data = excluded.Data;
                """;
            cmd.Parameters.AddWithValue("$id", document.Id.ToString());
            cmd.Parameters.AddWithValue("$data", json);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteByIdAsync(Guid noteGraphId)
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM NoteGraphDocuments WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", noteGraphId.ToString());
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
