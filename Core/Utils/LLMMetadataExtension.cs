using graphnotelm.Core.Models;
using System.Text.Json;

namespace graphnotelm.Core.Utils
{
    public static class LLMMetadataExtension
    {
        public static T? GetNamespace<T>(this NoteNodeMetadata metadata, string ns) where T : class
        {
            try
            {
                var document = JsonDocument.Parse(metadata.LLMMetadata);
                if (document.RootElement.TryGetProperty(ns, out var section))
                {
                    return JsonSerializer.Deserialize<T>(section.GetRawText());
                }
            }
            catch { }
            return null;
        }

        public static JsonElement? GetRawNamespace(this NoteNodeMetadata metadata, string ns)
        {
            if (string.IsNullOrWhiteSpace(metadata.LLMMetadata))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(metadata.LLMMetadata);

                if (doc.RootElement.TryGetProperty(ns, out var section))
                {
                    return section.Clone();
                }
            }
            catch (JsonException) { }

            return null;
        }

        public static void SetNamespace<T>(this NoteNodeMetadata metadata, string ns, T value)
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(metadata.LLMMetadata) ?? new();
            dict[ns] = JsonSerializer.SerializeToElement(value);
            metadata.LLMMetadata = JsonSerializer.Serialize(dict);
        }
    }
}
