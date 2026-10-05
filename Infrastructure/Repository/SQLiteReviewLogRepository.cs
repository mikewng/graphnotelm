using graphnotelm.Core.Models;
using graphnotelm.Infrastructure.Repository.Contracts;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace graphnotelm.Infrastructure.Repository
{
    public class SQLiteReviewLogRepository : IReviewLogRepository
    {
        private readonly string _connectionString;

        public SQLiteReviewLogRepository(IConfiguration configuration)
        {
            var raw = configuration.GetConnectionString("LocalDB")
                ?? throw new InvalidOperationException("LocalDB connection string missing");
            _connectionString = Environment.ExpandEnvironmentVariables(raw);
            EnsureTable();
        }

        // Created here rather than through EF, like the other local tables (see SQLiteFlashcardRepository).
        private void EnsureTable()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS ReviewLog (
                    ReviewId        TEXT NOT NULL PRIMARY KEY,
                    GraphId         TEXT NOT NULL,
                    NodeId          TEXT NOT NULL,
                    CardId          TEXT NULL,
                    Grade           INTEGER NOT NULL,
                    ReviewedAt      TEXT NOT NULL,
                    ElapsedDays     REAL NOT NULL,
                    Retrievability  REAL NULL,
                    StabilityAfter  REAL NOT NULL,
                    DifficultyAfter REAL NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_ReviewLog_GraphId_NodeId ON ReviewLog (GraphId, NodeId);
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

        public async Task AddAsync(ReviewLogEntry entry, CancellationToken ct = default)
        {
            await using var conn = await OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO ReviewLog (ReviewId, GraphId, NodeId, CardId, Grade, ReviewedAt, ElapsedDays, Retrievability, StabilityAfter, DifficultyAfter)
                VALUES ($reviewId, $graphId, $nodeId, $cardId, $grade, $reviewedAt, $elapsedDays, $retrievability, $stabilityAfter, $difficultyAfter);
                """;
            cmd.Parameters.AddWithValue("$reviewId", entry.Id.ToString());
            cmd.Parameters.AddWithValue("$graphId", entry.GraphId.ToString());
            cmd.Parameters.AddWithValue("$nodeId", entry.NodeId.ToString());
            cmd.Parameters.AddWithValue("$cardId", (object?)entry.CardId?.ToString() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$grade", (int)entry.Grade);
            cmd.Parameters.AddWithValue("$reviewedAt", entry.ReviewedAt.ToString("O", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$elapsedDays", entry.ElapsedDays);
            cmd.Parameters.AddWithValue("$retrievability", (object?)entry.Retrievability ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$stabilityAfter", entry.StabilityAfter);
            cmd.Parameters.AddWithValue("$difficultyAfter", entry.DifficultyAfter);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task<List<ReviewLogEntry>> GetByNodeAsync(Guid noteGraphId, Guid noteNodeId, CancellationToken ct = default)
        {
            await using var conn = await OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT ReviewId, GraphId, NodeId, CardId, Grade, ReviewedAt, ElapsedDays, Retrievability, StabilityAfter, DifficultyAfter
                FROM ReviewLog WHERE GraphId = $graphId AND NodeId = $nodeId
                ORDER BY ReviewedAt
                """;
            cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
            cmd.Parameters.AddWithValue("$nodeId", noteNodeId.ToString());
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var entries = new List<ReviewLogEntry>();
            while (await reader.ReadAsync(ct))
            {
                entries.Add(new ReviewLogEntry
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    GraphId = Guid.Parse(reader.GetString(1)),
                    NodeId = Guid.Parse(reader.GetString(2)),
                    CardId = reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3)),
                    Grade = (ReviewGrade)reader.GetInt32(4),
                    ReviewedAt = DateTime.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    ElapsedDays = reader.GetDouble(6),
                    Retrievability = reader.IsDBNull(7) ? null : reader.GetDouble(7),
                    StabilityAfter = reader.GetDouble(8),
                    DifficultyAfter = reader.GetDouble(9),
                });
            }
            return entries;
        }

        public async Task DeleteByNodeAsync(Guid noteGraphId, Guid noteNodeId, CancellationToken ct = default)
        {
            await using var conn = await OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM ReviewLog WHERE GraphId = $graphId AND NodeId = $nodeId";
            cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
            cmd.Parameters.AddWithValue("$nodeId", noteNodeId.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task DeleteByGraphAsync(Guid noteGraphId, CancellationToken ct = default)
        {
            await using var conn = await OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM ReviewLog WHERE GraphId = $graphId";
            cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
