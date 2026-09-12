using graphnotelm.Core.Models;

namespace graphnotelm.Infrastructure.Repository.Contracts
{
    public interface IImageRepository
    {
        // Persists the bytes and the metadata row together; sets image.StoragePath.
        public Task SaveAsync(NoteImage image, Stream content, CancellationToken ct = default);
        public Task<NoteImage?> GetByIdAsync(Guid imageId, CancellationToken ct = default);

        // Null when the stored bytes are missing. The caller owns (and disposes) the stream.
        public Task<Stream?> OpenReadAsync(NoteImage image, CancellationToken ct = default);

        public Task DeleteByNodeAsync(Guid noteGraphId, Guid noteNodeId, CancellationToken ct = default);
        public Task DeleteByGraphAsync(Guid noteGraphId, CancellationToken ct = default);
    }
}
