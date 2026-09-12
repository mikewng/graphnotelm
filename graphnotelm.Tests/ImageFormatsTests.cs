using graphnotelm.Core.Utils;

namespace graphnotelm.Tests
{
    public class ImageFormatsTests
    {
        public static TheoryData<byte[], string> KnownSignatures => new()
        {
            { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D }, "image/png" },
            { new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 }, "image/jpeg" },
            { "GIF87a\0\0\0\0\0\0"u8.ToArray(), "image/gif" },
            { "GIF89a\0\0\0\0\0\0"u8.ToArray(), "image/gif" },
            { "RIFF$\0\0\0WEBP"u8.ToArray(), "image/webp" },
        };

        [Theory]
        [MemberData(nameof(KnownSignatures))]
        public void Detect_KnownSignature_ReturnsFormat(byte[] header, string expectedContentType)
        {
            var format = ImageFormats.Detect(header);

            Assert.NotNull(format);
            Assert.Equal(expectedContentType, format!.ContentType);
        }

        public static TheoryData<byte[]> UnsupportedHeaders => new()
        {
            Array.Empty<byte>(),
            "hello world!"u8.ToArray(),
            "<svg xmlns=\"http://www.w3.org/2000/svg\">"u8.ToArray(),
            "<?xml version=\"1.0\"?>"u8.ToArray(),
            "BM6\0\0\0\0\0\0\06\0"u8.ToArray(), // BMP
            "RIFF$\0\0\0WAVE"u8.ToArray(),          // RIFF container that isn't WebP
            "RIFF$\0\0\0WEB"u8.ToArray(),           // truncated WebP header
            new byte[] { 0x89, 0x50, 0x4E },              // truncated PNG signature
        };

        [Theory]
        [MemberData(nameof(UnsupportedHeaders))]
        public void Detect_UnsupportedOrTruncatedHeader_ReturnsNull(byte[] header)
        {
            Assert.Null(ImageFormats.Detect(header));
        }

        [Theory]
        [InlineData("image/png", ".png")]
        [InlineData("IMAGE/JPEG", ".jpg")]
        [InlineData("image/gif", ".gif")]
        [InlineData("image/webp", ".webp")]
        public void FromContentType_KnownType_ReturnsFormat(string contentType, string expectedExtension)
        {
            Assert.Equal(expectedExtension, ImageFormats.FromContentType(contentType)?.Extension);
        }

        [Theory]
        [InlineData("image/svg+xml")]
        [InlineData("text/html")]
        [InlineData("")]
        public void FromContentType_UnsupportedType_ReturnsNull(string contentType)
        {
            Assert.Null(ImageFormats.FromContentType(contentType));
        }
    }
}
