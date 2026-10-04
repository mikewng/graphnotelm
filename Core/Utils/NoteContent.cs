using Markdig;

namespace graphnotelm.Core.Utils
{
    /// <summary>
    /// Notes are stored as the HTML the TipTap editor produces, but LLMs write Markdown,
    /// so AI-authored note content is converted here before it is saved.
    /// </summary>
    public static class NoteContent
    {
        // Strikethrough and links mirror what the editor's StarterKit can render. Single
        // newlines become <br> because LLM output relies on them as visible line breaks.
        // Raw HTML passes through untouched, so content already in editor HTML (e.g. read
        // back via get_node) survives; the editor's schema drops any tag it doesn't support.
        private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
            .UseEmphasisExtras(Markdig.Extensions.EmphasisExtras.EmphasisExtraOptions.Strikethrough)
            .UseAutoLinks()
            .UseSoftlineBreakAsHardlineBreak()
            .Build();

        public static string FromMarkdown(string markdown) =>
            string.IsNullOrWhiteSpace(markdown)
                ? string.Empty
                : Markdown.ToHtml(markdown, _pipeline).Trim();
    }
}
