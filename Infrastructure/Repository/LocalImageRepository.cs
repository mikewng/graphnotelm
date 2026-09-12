using graphnotelm.Core.Models;
using graphnotelm.Core.Utils;
using graphnotelm.Infrastructure.Repository.Contracts;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace graphnotelm.Infrastructure.Repository
{
    /// <summary>
    /// Stores image bytes on the local filesystem and their metadata in the local SQLite
    /// database. Files live at {root}/{graphId}/{imageId}{ext}; paths are built only from
    /// ids and the detected format, never from the uploaded file name.
    /// </summary>
    public class LocalImageRepository : IImageRepository
    {
        private readonly string _connectionString;
        private readonly string _rootPath;

        public LocalImageRepository(IConfiguration configuration)
        {
            var raw = configuration.GetConnectionString("LocalDB")
                ?? throw new InvalidOperationException("LocalDB connection string missing");
            _connectionString = Environment.ExpandEnvironmentVariables(raw);

            // Default to an "images" folder beside the database file.
            var configuredRoot = configuration["ImageStore:RootPath"];
            var dbPath = new SqliteConnectionStringBuilder(_connectionString).DataSource;
            _rootPath = Path.GetFullPath(!string.IsNullOrWhiteSpace(configuredRoot)
                ? Environment.ExpandEnvironmentVariables(configuredRoot)
                : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dbPath)) ?? ".", "images"));

            Directory.CreateDirectory(_rootPath);
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
                CREATE TABLE IF NOT EXISTS NoteImages (
                    ImageId          TEXT NOT NULL PRIMARY KEY,
                    GraphId          TEXT NOT NULL,
                    NodeId           TEXT NOT NULL,
                    OriginalFileName TEXT NOT NULL DEFAULT '',
                    ContentType      TEXT NOT NULL,
                    SizeBytes        INTEGER NOT NULL,
                    StoragePath      TEXT NOT NULL,
                    CreatedAt        TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_NoteImages_GraphId_NodeId ON NoteImages (GraphId, NodeId);
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

        // StoragePath comes from our own rows, but resolve defensively so a tampered row
        // can never point outside the image root.
        private string? ResolveFullPath(string storagePath)
        {
            var fullPath = Path.GetFullPath(Path.Combine(_rootPath, storagePath));
            var rootWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar) ? _rootPath : _rootPath + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) ? fullPath : null;
        }

        public async Task SaveAsync(NoteImage image, Stream content, CancellationToken ct = default)
        {
            var extension = ImageFormats.FromContentType(image.ContentType)?.Extension
                ?? throw new ArgumentException($"Unsupported content type: {image.ContentType}", nameof(image));

            var storagePath = $"{image.GraphId}/{image.Id}{extension}";
            var fullPath = ResolveFullPath(storagePath)!;
            var tempPath = fullPath + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            var moved = false;
            try
            {
                // Write to a temp file and move it into place, so a half-written upload
                // is never visible at the final path.
                await using (var file = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                    await content.CopyToAsync(file, ct);
                File.Move(tempPath, fullPath);
                moved = true;

                image.StoragePath = storagePath;

                await using var conn = await OpenConnectionAsync(ct);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO NoteImages (ImageId, GraphId, NodeId, OriginalFileName, ContentType, SizeBytes, StoragePath, CreatedAt)
                    VALUES ($imageId, $graphId, $nodeId, $fileName, $contentType, $sizeBytes, $storagePath, $createdAt);
                    """;
                cmd.Parameters.AddWithValue("$imageId", image.Id.ToString());
                cmd.Parameters.AddWithValue("$graphId", image.GraphId.ToString());
                cmd.Parameters.AddWithValue("$nodeId", image.NodeId.ToString());
                cmd.Parameters.AddWithValue("$fileName", image.OriginalFileName);
                cmd.Parameters.AddWithValue("$contentType", image.ContentType);
                cmd.Parameters.AddWithValue("$sizeBytes", image.SizeBytes);
                cmd.Parameters.AddWithValue("$storagePath", storagePath);
                cmd.Parameters.AddWithValue("$createdAt", image.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch
            {
                image.StoragePath = string.Empty;
                TryDeleteFile(tempPath);
                // Only remove the final file if this call put it there — a failed move means
                // the path already belonged to another image.
                if (moved)
                    TryDeleteFile(fullPath);
                throw;
            }
        }

        public async Task<NoteImage?> GetByIdAsync(Guid imageId, CancellationToken ct = default)
        {
            await using var conn = await OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT ImageId, GraphId, NodeId, OriginalFileName, ContentType, SizeBytes, StoragePath, CreatedAt
                FROM NoteImages WHERE ImageId = $imageId
                """;
            cmd.Parameters.AddWithValue("$imageId", imageId.ToString());
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            return new NoteImage
            {
                Id = Guid.Parse(reader.GetString(0)),
                GraphId = Guid.Parse(reader.GetString(1)),
                NodeId = Guid.Parse(reader.GetString(2)),
                OriginalFileName = reader.GetString(3),
                ContentType = reader.GetString(4),
                SizeBytes = reader.GetInt64(5),
                StoragePath = reader.GetString(6),
                CreatedAt = DateTime.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            };
        }

        public Task<Stream?> OpenReadAsync(NoteImage image, CancellationToken ct = default)
        {
            var fullPath = ResolveFullPath(image.StoragePath);
            if (fullPath is null || !File.Exists(fullPath))
                return Task.FromResult<Stream?>(null);

            Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            return Task.FromResult<Stream?>(stream);
        }

        public async Task DeleteByNodeAsync(Guid noteGraphId, Guid noteNodeId, CancellationToken ct = default)
        {
            await using var conn = await OpenConnectionAsync(ct);

            var storagePaths = new List<string>();
            await using (var select = conn.CreateCommand())
            {
                select.CommandText = "SELECT StoragePath FROM NoteImages WHERE GraphId = $graphId AND NodeId = $nodeId";
                select.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
                select.Parameters.AddWithValue("$nodeId", noteNodeId.ToString());
                await using var reader = await select.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    storagePaths.Add(reader.GetString(0));
            }

            // Rows first: a leftover file is harmless, a row pointing at a missing file is not.
            await using (var delete = conn.CreateCommand())
            {
                delete.CommandText = "DELETE FROM NoteImages WHERE GraphId = $graphId AND NodeId = $nodeId";
                delete.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
                delete.Parameters.AddWithValue("$nodeId", noteNodeId.ToString());
                await delete.ExecuteNonQueryAsync(ct);
            }

            foreach (var storagePath in storagePaths)
                if (ResolveFullPath(storagePath) is { } fullPath)
                    TryDeleteFile(fullPath);
        }

        public async Task DeleteByGraphAsync(Guid noteGraphId, CancellationToken ct = default)
        {
            await using (var conn = await OpenConnectionAsync(ct))
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM NoteImages WHERE GraphId = $graphId";
                cmd.Parameters.AddWithValue("$graphId", noteGraphId.ToString());
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // The graph folder also holds images whose rows were already orphaned, so
            // removing it wholesale leaves nothing behind.
            var graphDir = Path.Combine(_rootPath, noteGraphId.ToString());
            if (Directory.Exists(graphDir))
                Directory.Delete(graphDir, recursive: true);
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
