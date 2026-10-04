using graphnotelm.Core.Utils;

namespace graphnotelm.Tests
{
    public class NoteContentTests
    {
        [Fact]
        public void FromMarkdown_ConvertsBoldListItemsToEditorHtml()
        {
            var html = NoteContent.FromMarkdown(
                "A camera-translate app is a lifesaver.\n" +
                "- **Google Translate** – works only with a VPN\n" +
                "- **Pleco** – works offline");

            Assert.Equal(
                "<p>A camera-translate app is a lifesaver.</p>\n" +
                "<ul>\n" +
                "<li><strong>Google Translate</strong> – works only with a VPN</li>\n" +
                "<li><strong>Pleco</strong> – works offline</li>\n" +
                "</ul>",
                html);
        }

        [Fact]
        public void FromMarkdown_SingleNewlineBecomesLineBreak()
        {
            Assert.Equal("<p>line one<br />\nline two</p>", NoteContent.FromMarkdown("line one\nline two"));
        }

        [Fact]
        public void FromMarkdown_SupportsStrikethroughAndCodeBlocks()
        {
            var html = NoteContent.FromMarkdown("~~old~~\n\n```js\nlet x = 1\n```");

            Assert.Contains("<del>old</del>", html);
            Assert.Contains("<pre><code class=\"language-js\">let x = 1\n</code></pre>", html);
        }

        [Fact]
        public void FromMarkdown_PassesEditorHtmlThrough()
        {
            const string editorHtml = "<p>Hello <strong>there</strong></p><ul><li><p>item</p></li></ul>";

            Assert.Equal(editorHtml, NoteContent.FromMarkdown(editorHtml));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   \n ")]
        public void FromMarkdown_BlankInput_ReturnsEmpty(string input)
        {
            Assert.Equal(string.Empty, NoteContent.FromMarkdown(input));
        }
    }
}
