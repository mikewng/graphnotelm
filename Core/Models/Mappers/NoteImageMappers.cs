using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Utils;

namespace graphnotelm.Core.Models.Mappers
{
    public static class NoteImageMapper
    {
        private const int MaxFileNameLength = 255;

        // Matches NoteNodeController.GetImage.
        public static string ToImageUrl(Guid imageId) => $"/NoteGraph/images/{imageId}";

        public static NoteImage ToNoteImage(this UploadImageRequest request, Guid id, Guid graphId, Guid nodeId, ImageFormat format, long sizeBytes)
        {
            // Strip any directory components a client may send; the name is display-only.
            var fileName = Path.GetFileName(request.FileName ?? string.Empty);
            if (fileName.Length > MaxFileNameLength)
                fileName = fileName[..MaxFileNameLength];

            return new NoteImage
            {
                Id = id,
                GraphId = graphId,
                NodeId = nodeId,
                OriginalFileName = fileName,
                ContentType = format.ContentType,
                SizeBytes = sizeBytes
            };
        }

        public static UploadImageResponse ToUploadImageResponse(this NoteImage image)
            => new UploadImageResponse { ImageId = image.Id, Url = ToImageUrl(image.Id) };
    }
}
