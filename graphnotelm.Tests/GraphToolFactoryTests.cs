using graphnotelm.Core.Services;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Core.Utils;
using Moq;

namespace graphnotelm.Tests
{
    public class GraphToolFactoryTests
    {
        [Fact]
        public void Build_ReturnsFullToolset()
        {
            var factory = new GraphToolFactory(new Mock<IGraphAnalysisService>().Object);
            var document = TestData.NewDocument(Guid.NewGuid());

            var tools = factory.Build(document, new GraphView(document));

            Assert.Equal(8, tools.Count);
        }

        [Fact]
        public void Build_ToolNamesAreUniqueAndDescribed()
        {
            var factory = new GraphToolFactory(new Mock<IGraphAnalysisService>().Object);
            var document = TestData.NewDocument(Guid.NewGuid());

            var tools = factory.Build(document, new GraphView(document));

            Assert.Equal(tools.Count, tools.Select(t => t.Name).Distinct().Count());
            Assert.All(tools, t => Assert.False(string.IsNullOrWhiteSpace(t.Name)));
            Assert.All(tools, t => Assert.False(string.IsNullOrWhiteSpace(t.Description)));
        }

        [Fact]
        public async Task Build_TagListTool_ReadsFromDocument()
        {
            var factory = new GraphToolFactory(new Mock<IGraphAnalysisService>().Object);
            var document = TestData.NewDocument(Guid.NewGuid());
            document.Tags[Guid.NewGuid()] = new Core.Models.TagDefinition { Name = "physics" };

            var tools = factory.Build(document, new GraphView(document));
            var tagTool = tools.Single(t => t.Name.Contains("Tags", StringComparison.OrdinalIgnoreCase)
                                         && !t.Name.Contains("Node", StringComparison.OrdinalIgnoreCase));

            var result = await tagTool.InvokeAsync(new Microsoft.Extensions.AI.AIFunctionArguments());

            // AIFunction serializes return values to JSON
            var json = Assert.IsType<System.Text.Json.JsonElement>(result);
            var tags = json.EnumerateArray().Select(e => e.GetString()).ToList();
            Assert.Equal(new[] { "physics" }, tags);
        }
    }
}
