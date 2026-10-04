using graphnotelm.Core.Models;
using graphnotelm.Infrastructure.Repository;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace graphnotelm.Tests
{
    /// <summary>
    /// Runs the flashcard, review log and note memory storage against a real SQLite file
    /// in a per-test temp directory.
    /// </summary>
    public class StudyRepositoriesTests : IDisposable
    {
        private static readonly DateTime Now = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

        private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "graphnotelm-tests", Guid.NewGuid().ToString());
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly Guid _graphId = Guid.NewGuid();

        public StudyRepositoriesTests()
        {
            Directory.CreateDirectory(_tempDir);
            _connectionString = $"Data Source={Path.Combine(_tempDir, "graphnotelm.db")}";
            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:LocalDB"] = _connectionString })
                .Build();
        }

        public void Dispose()
        {
            // Pooled connections keep the database file locked on Windows.
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(_tempDir, recursive: true); }
            catch (IOException) { }
        }

        private Flashcard NewCard(Guid nodeId, string front, int minutesAgo = 0, Guid? graphId = null) => new()
        {
            Id = Guid.NewGuid(),
            GraphId = graphId ?? _graphId,
            NodeId = nodeId,
            Front = front,
            Back = front + " answer",
            CreatedAt = Now.AddMinutes(-minutesAgo),
            UpdatedAt = Now.AddMinutes(-minutesAgo)
        };

        [Fact]
        public async Task Flashcards_SaveReadUpdateAndDelete()
        {
            var repo = new SQLiteFlashcardRepository(_configuration);
            var nodeA = Guid.NewGuid();
            var nodeB = Guid.NewGuid();
            var second = NewCard(nodeA, "Second", minutesAgo: 1);
            var first = NewCard(nodeA, "First", minutesAgo: 5);
            var onB = NewCard(nodeB, "On B");
            var otherGraph = NewCard(nodeA, "Other graph", graphId: Guid.NewGuid());
            foreach (var card in new[] { second, first, onB, otherGraph })
                await repo.SaveAsync(card);

            Assert.Equal(new[] { "First", "Second" }, (await repo.GetByNodeAsync(_graphId, nodeA)).Select(c => c.Front));
            Assert.Equal(3, (await repo.GetByGraphAsync(_graphId)).Count);

            first.Front = "First, edited";
            first.UpdatedAt = Now.AddMinutes(10);
            first.CreatedAt = Now.AddYears(1); // must not overwrite the stored creation time
            await repo.SaveAsync(first);
            var stored = await repo.GetByIdAsync(_graphId, first.Id);
            Assert.Equal("First, edited", stored!.Front);
            Assert.Equal(Now.AddMinutes(10), stored.UpdatedAt);
            Assert.Equal(Now.AddMinutes(-5), stored.CreatedAt);
            Assert.Equal(DateTimeKind.Utc, stored.CreatedAt.Kind);

            await repo.DeleteAsync(_graphId, second.Id);
            Assert.Single(await repo.GetByNodeAsync(_graphId, nodeA));

            await repo.DeleteByNodeAsync(_graphId, nodeA);
            Assert.Empty(await repo.GetByNodeAsync(_graphId, nodeA));
            Assert.Single(await repo.GetByGraphAsync(_graphId));

            await repo.DeleteByGraphAsync(_graphId);
            Assert.Empty(await repo.GetByGraphAsync(_graphId));
            Assert.Single(await repo.GetByGraphAsync(otherGraph.GraphId));
        }

        [Fact]
        public async Task Flashcards_GetByIdIsScopedToTheGraph()
        {
            var repo = new SQLiteFlashcardRepository(_configuration);
            var card = NewCard(Guid.NewGuid(), "Card");
            await repo.SaveAsync(card);

            Assert.NotNull(await repo.GetByIdAsync(_graphId, card.Id));
            Assert.Null(await repo.GetByIdAsync(Guid.NewGuid(), card.Id));
        }

        [Fact]
        public async Task Flashcards_SaveManyInsertsEveryCard()
        {
            var repo = new SQLiteFlashcardRepository(_configuration);
            var node = Guid.NewGuid();

            await repo.SaveManyAsync(new[] { NewCard(node, "One"), NewCard(node, "Two"), NewCard(node, "Three") });

            Assert.Equal(3, (await repo.GetByNodeAsync(_graphId, node)).Count);
        }

        [Fact]
        public async Task ReviewLog_AddReadAndDelete()
        {
            var repo = new SQLiteReviewLogRepository(_configuration);
            var node = Guid.NewGuid();
            var cardId = Guid.NewGuid();
            var later = new ReviewLogEntry
            {
                Id = Guid.NewGuid(), GraphId = _graphId, NodeId = node, CardId = cardId, Grade = ReviewGrade.Hard,
                ReviewedAt = Now, ElapsedDays = 4.5, Retrievability = 0.87, StabilityAfter = 6.1, DifficultyAfter = 5.8
            };
            var first = new ReviewLogEntry
            {
                Id = Guid.NewGuid(), GraphId = _graphId, NodeId = node, CardId = null, Grade = ReviewGrade.Good,
                ReviewedAt = Now.AddDays(-4.5), ElapsedDays = 0, Retrievability = null, StabilityAfter = 3.7, DifficultyAfter = 5.2
            };
            await repo.AddAsync(later);
            await repo.AddAsync(first);
            await repo.AddAsync(new ReviewLogEntry { Id = Guid.NewGuid(), GraphId = _graphId, NodeId = Guid.NewGuid(), Grade = ReviewGrade.Easy, ReviewedAt = Now });

            var entries = await repo.GetByNodeAsync(_graphId, node);

            Assert.Equal(new[] { first.Id, later.Id }, entries.Select(e => e.Id));
            Assert.Null(entries[0].CardId);
            Assert.Null(entries[0].Retrievability);
            Assert.Equal((cardId, ReviewGrade.Hard, 4.5, 0.87), (entries[1].CardId!.Value, entries[1].Grade, entries[1].ElapsedDays, entries[1].Retrievability!.Value));
            Assert.Equal(Now, entries[1].ReviewedAt);

            await repo.DeleteByNodeAsync(_graphId, node);
            Assert.Empty(await repo.GetByNodeAsync(_graphId, node));

            await repo.DeleteByGraphAsync(_graphId);
        }

        [Fact]
        public async Task NoteNodes_MemoryRoundTrips_AndBlobsWrittenBeforeReviewsLoadAsNeverReviewed()
        {
            var repo = new SQLiteNoteNodeRepository(_configuration);
            var reviewed = TestData.NewNode("Reviewed");
            reviewed.Metadata.Memory = new MemoryState
            {
                Stability = 12.5, Difficulty = 4.2, LastReviewedAt = Now, DueAt = Now.AddDays(13), Reviews = 3, Lapses = 1
            };
            await repo.SaveAsync(_graphId, reviewed);

            // A row as written by earlier versions, whose metadata blob has no Memory key at all.
            var legacyId = Guid.NewGuid();
            await using (var conn = new SqliteConnection(_connectionString))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO NoteNodes (GraphId, NodeId, Title, Note, IsPinned, FolderId, Metadata)
                    VALUES ($graphId, $nodeId, 'Legacy', '', 0, NULL, '{"UserConfidenceRate":6,"LLMMetadata":"","Relationships":[],"Tags":[]}');
                    """;
                cmd.Parameters.AddWithValue("$graphId", _graphId.ToString());
                cmd.Parameters.AddWithValue("$nodeId", legacyId.ToString());
                await cmd.ExecuteNonQueryAsync();
            }

            var memory = (await repo.GetByIdAsync(_graphId, reviewed.Id))!.Metadata.Memory;
            Assert.NotNull(memory);
            Assert.Equal((12.5, 4.2, 3, 1), (memory!.Stability, memory.Difficulty, memory.Reviews, memory.Lapses));
            Assert.Equal(Now.AddDays(13), memory.DueAt);
            Assert.Equal(DateTimeKind.Utc, memory.DueAt.Kind);

            var skeletons = await repo.GetAllSkeletonsByGraphIdAsync(_graphId);
            Assert.NotNull(skeletons.Single(n => n.Id == reviewed.Id).Metadata.Memory);
            var legacy = skeletons.Single(n => n.Id == legacyId);
            Assert.Null(legacy.Metadata.Memory);
            Assert.Equal(6f, legacy.Metadata.UserConfidenceRate);
        }
    }
}
