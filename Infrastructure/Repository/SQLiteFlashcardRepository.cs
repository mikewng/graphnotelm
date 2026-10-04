using graphnotelm.Core.Models;
using graphnotelm.Infrastructure.Repository.Contracts;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace graphnotelm.Infrastructure.Repository
{
    public class SQLiteFlashcardRepository : IFlashcardRepository
    {
        private readonly string _connectionString;

        private const string Columns = "CardId, GraphId, NodeId, Front, Back, CreatedAt, UpdatedAt";

        private const string UpsertSql = """
            INSERT INTO Flashcards (CardId, GraphId, NodeId, Front, Back, CreatedAt, UpdatedAt)
            VALUES ($cardId, $graphId, $nodeId, $front, $back, $createdAt, $updatedAt)
            ON CONFLICT(CardId) DO UPDATE SET
                Front     = excluded.Front,
                Back      = excluded.Back,
                UpdatedAt = excluded.UpdatedAt;
            """;

        public SQLiteFlashcardRepository(IConfiguration configuration)
        {
            var raw = configuration.GetConnectionString("LocalDB")
                ?? throw new InvalidOperationException("LocalDB connection string missing");
            _connectionString = Environment.ExpandEnvironmentVariables(raw);
            EnsureTable();
        }

        // Local mode initializes the EF schema with EnsureCreated, which is a no-op on an
        // existing database file — so this table must create itself, like NoteNodes does.
        private void EnsureTable()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS Flashcards (
                    CardId    TEXT NOT NULL PRIMARY KEY,
                    GraphId   TEXT NOT NULL,
                    NodeId    TEXT NOT NULL,
                    Front     TEXT NOT NULL,
                    Back      TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_Flashcards_GraphId_NodeId ON Flashcards (GraphId, NodeId);
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

        private static string FormatDate(DateTime value) => value.ToString("O", CultureInfo.InvariantCulture);

        private static DateTime ParseDate(string value) => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        private static Flashcard Read(SqliteDataReader reader) => new()
        {
            Id = Guid.Parse(reader.GetString(0)),
            GraphId = Guid.Parse(reader.GetString(1)),
            NodeId = Guid.Parse(reader.GetString(2)),
            Front = reader.GetString(3),
            Back = reader.GetString(4),
            CreatedAt = ParseDate(reader.GetString(5)),
            UpdatedAt = ParseDate(reader.GetString(6)),
        };

        private async Task<List<Flashcard>> QueryAsync(string where, Action<SqliteCommand> bind, CancellationToken ct)
        {
            await using var conn = await OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT {Columns} FROM Flashcards WHERE {where} ORDER BY CreatedAt, CardId";
            bind(cmd);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var cards = new List<Flashcard>();
            while (await reader.ReadAsync(ct))
                cards.Add(Read(reader));
            return cards;
        }

        public Task<List<Flashcard>> GetByNodeAsync(Guid noteGraphId, Guid noteNodeId, CancellationToken ct = default)
            => QueryAsync("GraphId = $graphId AND NodeId = $nodeId", cmd =>
            {
                cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
                cmd.Parameters.AddWithValue("$nodeId", noteNodeId.ToString());
            }, ct);

        public Task<List<Flashcard>> GetByGraphAsync(Guid noteGraphId, CancellationToken ct = default)
            => QueryAsync("GraphId = $graphId", cmd => cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString()), ct);

        public async Task<Flashcard?> GetByIdAsync(Guid noteGraphId, Guid cardId, CancellationToken ct = default)
            => (await QueryAsync("GraphId = $graphId AND CardId = $cardId", cmd =>
            {
                cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
                cmd.Parameters.AddWithValue("$cardId", cardId.ToString());
            }, ct)).FirstOrDefault();

        private static void BindCard(SqliteCommand cmd, Flashcard card)
        {
            cmd.Parameters.AddWithValue("$cardId", card.Id.ToString());
            cmd.Parameters.AddWithValue("$graphId", card.GraphId.ToString());
            cmd.Parameters.AddWithValue("$nodeId", card.NodeId.ToString());
            cmd.Parameters.AddWithValue("$front", card.Front);
            cmd.Parameters.AddWithValue("$back", card.Back);
            cmd.Parameters.AddWithValue("$createdAt", FormatDate(card.CreatedAt));
            cmd.Parameters.AddWithValue("$updatedAt", FormatDate(card.UpdatedAt));
        }

        public async Task SaveAsync(Flashcard card, CancellationToken ct = default)
        {
            await using var conn = await OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = UpsertSql;
            BindCard(cmd, card);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task SaveManyAsync(IEnumerable<Flashcard> cards, CancellationToken ct = default)
        {
            await using var conn = await OpenConnectionAsync(ct);
            await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);
            foreach (var card in cards)
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = UpsertSql;
                BindCard(cmd, card);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
        }

        private async Task ExecuteAsync(string sql, Action<SqliteCommand> bind, CancellationToken ct)
        {
            await using var conn = await OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            bind(cmd);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public Task DeleteAsync(Guid noteGraphId, Guid cardId, CancellationToken ct = default)
            => ExecuteAsync("DELETE FROM Flashcards WHERE GraphId = $graphId AND CardId = $cardId", cmd =>
            {
                cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
                cmd.Parameters.AddWithValue("$cardId", cardId.ToString());
            }, ct);

        public Task DeleteByNodeAsync(Guid noteGraphId, Guid noteNodeId, CancellationToken ct = default)
            => ExecuteAsync("DELETE FROM Flashcards WHERE GraphId = $graphId AND NodeId = $nodeId", cmd =>
            {
                cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
                cmd.Parameters.AddWithValue("$nodeId", noteNodeId.ToString());
            }, ct);

        public Task DeleteByGraphAsync(Guid noteGraphId, CancellationToken ct = default)
            => ExecuteAsync("DELETE FROM Flashcards WHERE GraphId = $graphId",
                cmd => cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString()), ct);
    }
}
