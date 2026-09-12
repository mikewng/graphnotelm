using graphnotelm.Core.Models;
using graphnotelm.Infrastructure.Repository;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace graphnotelm.Tests
{
    /// <summary>
    /// Runs against a real SQLite file and image folder in a per-test temp directory.
    /// </summary>
    public class LocalImageRepositoryTests : IDisposable
    {
        private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "graphnotelm-tests", Guid.NewGuid().ToString());
        private readonly string _dbPath;
        private readonly string _defaultImageRoot;
        private readonly Guid _graphId = Guid.NewGuid();

        public LocalImageRepositoryTests()
        {
            Directory.CreateDirectory(_tempDir);
            _dbPath = Path.Combine(_tempDir, "graphnotelm.db");
            _defaultImageRoot = Path.Combine(_tempDir, "images");
        }

        public void Dispose()
        {
            // Pooled connections keep the database file locked on Windows.
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(_tempDir, recursive: true); }
            catch (IOException) { }
        }

        private LocalImageRepository CreateRepository(string? imageRoot = null)
        {
            var values = new Dictionary<string, string?> { ["ConnectionStrings:LocalDB"] = $"Data Source={_dbPath}" };
            if (imageRoot is not null)
                values["ImageStore:RootPath"] = imageRoot;
            return new LocalImageRepository(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        }

        private NoteImage NewImage(Guid? graphId = null, Guid? nodeId = null, Guid? id = null) => new()
        {
            Id = id ?? Guid.NewGuid(),
            GraphId = graphId ?? _graphId,
            NodeId = nodeId ?? Guid.NewGuid(),
            OriginalFileName = "photo.png",
            ContentType = "image/png",
            SizeBytes = 64
        };

        private static async Task<NoteImage> SaveAsync(LocalImageRepository repo, NoteImage image, byte[]? bytes = null)
        {
            await repo.SaveAsync(image, new MemoryStream(bytes ?? TestData.PngBytes()));
            return image;
        }

        private static async Task<byte[]?> ReadBytesAsync(LocalImageRepository repo, NoteImage image)
        {
            await using var stream = await repo.OpenReadAsync(image);
            if (stream is null) return null;
            using var copy = new MemoryStream();
            await stream.CopyToAsync(copy);
            return copy.ToArray();
        }

        private string FilePathFor(NoteImage image, string? root = null)
            => Path.Combine(root ?? _defaultImageRoot, image.GraphId.ToString(), $"{image.Id}.png");

        // ---------- Construction ----------

        [Fact]
        public void Constructor_ExistingDatabaseWithoutImageTable_CreatesTable()
        {
            // Regression guard: local mode uses EnsureCreated, which never adds tables to an
            // existing database, so the repository has to create NoteImages itself.
            using (var conn = new SqliteConnection($"Data Source={_dbPath}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "CREATE TABLE NoteNodes (GraphId TEXT NOT NULL, NodeId TEXT NOT NULL);";
                cmd.ExecuteNonQuery();
            }

            CreateRepository();

            using var check = new SqliteConnection($"Data Source={_dbPath}");
            check.Open();
            using var query = check.CreateCommand();
            query.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'NoteImages'";
            Assert.Equal(1L, (long)query.ExecuteScalar()!);
        }

        [Fact]
        public void Constructor_CalledTwice_IsIdempotent()
        {
            CreateRepository();
            var exception = Record.Exception(() => CreateRepository());
            Assert.Null(exception);
        }

        // ---------- SaveAsync / GetByIdAsync / OpenReadAsync ----------

        [Fact]
        public async Task Save_ThenGetAndOpen_RoundTripsMetadataAndBytes()
        {
            var repo = CreateRepository();
            var bytes = TestData.PngBytes(256);
            var image = await SaveAsync(repo, NewImage(), bytes);

            var loaded = await repo.GetByIdAsync(image.Id);

            Assert.NotNull(loaded);
            Assert.Equal(image.GraphId, loaded!.GraphId);
            Assert.Equal(image.NodeId, loaded.NodeId);
            Assert.Equal("photo.png", loaded.OriginalFileName);
            Assert.Equal("image/png", loaded.ContentType);
            Assert.Equal(image.SizeBytes, loaded.SizeBytes);
            Assert.Equal($"{image.GraphId}/{image.Id}.png", loaded.StoragePath);
            Assert.Equal(image.CreatedAt, loaded.CreatedAt);
            Assert.Equal(bytes, await ReadBytesAsync(repo, loaded));
        }

        [Fact]
        public async Task Save_DefaultRoot_WritesBesideDatabaseWithoutTempFiles()
        {
            var repo = CreateRepository();
            var image = await SaveAsync(repo, NewImage());

            Assert.True(File.Exists(FilePathFor(image)));
            Assert.Empty(Directory.GetFiles(_defaultImageRoot, "*.tmp", SearchOption.AllDirectories));
        }

        [Fact]
        public async Task Save_ConfiguredRoot_WritesUnderConfiguredRoot()
        {
            var customRoot = Path.Combine(_tempDir, "custom-images");
            var repo = CreateRepository(customRoot);
            var image = await SaveAsync(repo, NewImage());

            Assert.True(File.Exists(FilePathFor(image, customRoot)));
        }

        [Fact]
        public async Task Save_PathIsBuiltFromIdsNotFileName()
        {
            var repo = CreateRepository();
            var image = NewImage();
            image.OriginalFileName = "../../../evil.png";

            await SaveAsync(repo, image);

            Assert.Equal($"{image.GraphId}/{image.Id}.png", image.StoragePath);
            Assert.Empty(Directory.GetFiles(_tempDir, "evil.png", SearchOption.AllDirectories));
        }

        [Fact]
        public async Task Save_UnsupportedContentType_Throws()
        {
            var repo = CreateRepository();
            var image = NewImage();
            image.ContentType = "image/svg+xml";

            await Assert.ThrowsAsync<ArgumentException>(() => repo.SaveAsync(image, new MemoryStream(TestData.PngBytes())));
        }

        [Fact]
        public async Task Save_RowInsertFails_RemovesWrittenFile()
        {
            var repo = CreateRepository();
            var original = await SaveAsync(repo, NewImage());

            // Same id (primary key clash) but a different graph, so the file path is new
            // and the failure happens at the row insert.
            var clash = NewImage(graphId: Guid.NewGuid(), id: original.Id);
            await Assert.ThrowsAsync<SqliteException>(() => repo.SaveAsync(clash, new MemoryStream(TestData.PngBytes())));

            Assert.False(File.Exists(FilePathFor(clash)));
            Assert.Equal(string.Empty, clash.StoragePath);
            Assert.True(File.Exists(FilePathFor(original)));
        }

        [Fact]
        public async Task Save_FilePathAlreadyTaken_ThrowsAndKeepsExistingFile()
        {
            var repo = CreateRepository();
            var originalBytes = TestData.PngBytes(100);
            var original = await SaveAsync(repo, NewImage(), originalBytes);

            var duplicate = NewImage(id: original.Id);
            await Assert.ThrowsAsync<IOException>(() => repo.SaveAsync(duplicate, new MemoryStream(TestData.PngBytes(200))));

            Assert.Equal(originalBytes, await File.ReadAllBytesAsync(FilePathFor(original)));
        }

        [Fact]
        public async Task GetById_UnknownId_ReturnsNull()
        {
            var repo = CreateRepository();

            Assert.Null(await repo.GetByIdAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task OpenRead_FileMissing_ReturnsNull()
        {
            var repo = CreateRepository();
            var image = await SaveAsync(repo, NewImage());
            File.Delete(FilePathFor(image));

            Assert.Null(await repo.OpenReadAsync(image));
        }

        [Fact]
        public async Task OpenRead_StoragePathOutsideRoot_ReturnsNull()
        {
            var repo = CreateRepository();
            var outsidePath = Path.Combine(_tempDir, "outside.png");
            await File.WriteAllBytesAsync(outsidePath, TestData.PngBytes());
            var image = NewImage();
            image.StoragePath = "../outside.png";

            Assert.Null(await repo.OpenReadAsync(image));
        }

        // ---------- Deletion ----------

        [Fact]
        public async Task DeleteByNode_RemovesOnlyThatNodesImages()
        {
            var repo = CreateRepository();
            var nodeId = Guid.NewGuid();
            var first = await SaveAsync(repo, NewImage(nodeId: nodeId));
            var second = await SaveAsync(repo, NewImage(nodeId: nodeId));
            var otherNode = await SaveAsync(repo, NewImage());

            await repo.DeleteByNodeAsync(_graphId, nodeId);

            Assert.Null(await repo.GetByIdAsync(first.Id));
            Assert.Null(await repo.GetByIdAsync(second.Id));
            Assert.False(File.Exists(FilePathFor(first)));
            Assert.False(File.Exists(FilePathFor(second)));
            Assert.NotNull(await repo.GetByIdAsync(otherNode.Id));
            Assert.True(File.Exists(FilePathFor(otherNode)));
        }

        [Fact]
        public async Task DeleteByGraph_RemovesRowsAndFolderButLeavesOtherGraphs()
        {
            var repo = CreateRepository();
            var inGraph = await SaveAsync(repo, NewImage());
            var otherGraph = await SaveAsync(repo, NewImage(graphId: Guid.NewGuid()));

            await repo.DeleteByGraphAsync(_graphId);

            Assert.Null(await repo.GetByIdAsync(inGraph.Id));
            Assert.False(Directory.Exists(Path.Combine(_defaultImageRoot, _graphId.ToString())));
            Assert.NotNull(await repo.GetByIdAsync(otherGraph.Id));
            Assert.True(File.Exists(FilePathFor(otherGraph)));
        }

        [Fact]
        public async Task DeleteByGraph_NoImages_DoesNotThrow()
        {
            var repo = CreateRepository();

            var exception = await Record.ExceptionAsync(() => repo.DeleteByGraphAsync(Guid.NewGuid()));

            Assert.Null(exception);
        }
    }
}
