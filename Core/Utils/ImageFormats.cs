namespace graphnotelm.Core.Utils
{
    public record ImageFormat(string ContentType, string Extension);

    /// <summary>
    /// Identifies uploaded images by their leading bytes rather than the client-supplied
    /// content type or file extension, both of which are trivially spoofed. SVG is
    /// deliberately unsupported: it can carry script that would run on the API origin.
    /// </summary>
    public static class ImageFormats
    {
        public const long MaxUploadBytes = 10 * 1024 * 1024;

        // Longest signature below is WebP: "RIFF" + 4 size bytes + "WEBP".
        public const int HeaderLength = 12;

        public static readonly ImageFormat Png = new("image/png", ".png");
        public static readonly ImageFormat Jpeg = new("image/jpeg", ".jpg");
        public static readonly ImageFormat Gif = new("image/gif", ".gif");
        public static readonly ImageFormat Webp = new("image/webp", ".webp");

        private static readonly ImageFormat[] _all = { Png, Jpeg, Gif, Webp };

        private static readonly byte[] _pngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly byte[] _jpegSignature = { 0xFF, 0xD8, 0xFF };
        private static readonly byte[] _gif87Signature = "GIF87a"u8.ToArray();
        private static readonly byte[] _gif89Signature = "GIF89a"u8.ToArray();
        private static readonly byte[] _riffSignature = "RIFF"u8.ToArray();
        private static readonly byte[] _webpSignature = "WEBP"u8.ToArray();

        public static ImageFormat? Detect(ReadOnlySpan<byte> header)
        {
            if (header.StartsWith(_pngSignature)) return Png;
            if (header.StartsWith(_jpegSignature)) return Jpeg;
            if (header.StartsWith(_gif87Signature) || header.StartsWith(_gif89Signature)) return Gif;
            if (header.Length >= HeaderLength && header.StartsWith(_riffSignature) && header[8..12].SequenceEqual(_webpSignature))
                return Webp;
            return null;
        }

        public static ImageFormat? FromContentType(string contentType)
            => _all.FirstOrDefault(f => string.Equals(f.ContentType, contentType, StringComparison.OrdinalIgnoreCase));
    }
}
