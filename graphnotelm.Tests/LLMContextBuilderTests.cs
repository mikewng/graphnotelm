using graphnotelm.Core.Models;
using graphnotelm.Core.Services;
using graphnotelm.Core.Utils;

namespace graphnotelm.Tests
{
    public class LLMContextBuilderTests
    {
        private readonly LLMContextBuilder _builder = new();

        // ---------- BuildNodeAnalysisPrompt ----------

        [Fact]
        public void NodeAnalysisPrompt_IncludesNodeContentAndSchemaHint()
        {
            var document = TestData.NewDocument(Guid.NewGuid());
            document.Context.MetadataSchemaHint = "summary, difficulty";
            var node = TestData.NewNode("Target Node", "target note body");
            node.Metadata.UserConfidenceRate = 0.7f;
            document.Nodes[node.Id] = node;

            var prompt = _builder.BuildNodeAnalysisPrompt(document, new GraphView(document), node.Id);

            Assert.Contains("summary, difficulty", prompt.System);
            Assert.Contains("Target Node", prompt.User);
            Assert.Contains("target note body", prompt.User);
            Assert.Contains("0.7", prompt.User);
        }

        [Fact]
        public void NodeAnalysisPrompt_IncludesOutgoingAndIncomingNeighbors()
        {
            var document = TestData.NewDocument(Guid.NewGuid());
            var relId = Guid.NewGuid();
            document.Relationships[relId] = new RelationshipDefinition { Name = "depends on" };

            var target = TestData.NewNode("Center", "center note");
            var downstream = TestData.NewNode("Downstream", "downstream note");
            var upstream = TestData.NewNode("Upstream", "upstream note");
            target.Relationships.Add(new NodeRelationship { TargetNodeId = downstream.Id, RelationshipId = relId });
            upstream.Relationships.Add(new NodeRelationship { TargetNodeId = target.Id, RelationshipId = relId });
            document.Nodes[target.Id] = target;
            document.Nodes[downstream.Id] = downstream;
            document.Nodes[upstream.Id] = upstream;

            var prompt = _builder.BuildNodeAnalysisPrompt(document, new GraphView(document), target.Id);

            Assert.Contains("THIS NODE [depends on] → Downstream", prompt.User);
            Assert.Contains("Upstream [depends on] → THIS NODE", prompt.User);
        }

        // ---------- BuildNodeFromPastePrompt ----------

        [Fact]
        public void NodeFromPastePrompt_ListsTagsRelationshipsAndNodes()
        {
            var document = TestData.NewDocument(Guid.NewGuid());
            var tagId = Guid.NewGuid();
            var relId = Guid.NewGuid();
            document.Tags[tagId] = new TagDefinition { Name = "physics" };
            document.Relationships[relId] = new RelationshipDefinition { Name = "explains", Inverse = "explained by" };
            var existing = TestData.NewNode("Existing Node");
            document.Nodes[existing.Id] = existing;

            var prompt = _builder.BuildNodeFromPastePrompt(document, "pasted body");

            Assert.Contains("pasted body", prompt.User);
            Assert.Contains(tagId.ToString(), prompt.User);
            Assert.Contains("physics", prompt.User);
            Assert.Contains(relId.ToString(), prompt.User);
            Assert.Contains("explains", prompt.User);
            Assert.Contains(existing.Id.ToString(), prompt.User);
            Assert.Contains("Existing Node", prompt.User);
        }

        [Fact]
        public void NodeFromPastePrompt_EmptyGraph_MarksSectionsAsNone()
        {
            var document = TestData.NewDocument(Guid.NewGuid());

            var prompt = _builder.BuildNodeFromPastePrompt(document, "pasted body");

            Assert.Contains("(none)", prompt.User);
            Assert.NotEmpty(prompt.System);
        }

        // ---------- BuildGraphExtractionPass1Prompt ----------

        [Fact]
        public void ExtractionPass1Prompt_UserMessageIsTheRawContent()
        {
            var prompt = _builder.BuildGraphExtractionPass1Prompt("full document text");

            Assert.Equal("full document text", prompt.User);
            Assert.Contains("relationshipTypes", prompt.System);
            Assert.Contains("graphName", prompt.System);
        }

        // ---------- BuildGraphExtractionPass2Prompt ----------

        [Fact]
        public void ExtractionPass2Prompt_ListsNodeAndRelationshipGuids()
        {
            var node = TestData.NewNode("Node A");
            var relTypeId = Guid.NewGuid();
            var nodes = new Dictionary<Guid, NoteNode> { [node.Id] = node };
            var relTypes = new Dictionary<Guid, RelationshipDefinition>
            {
                [relTypeId] = new RelationshipDefinition { Name = "causes", Inverse = "caused by" }
            };

            var prompt = _builder.BuildGraphExtractionPass2Prompt("original text", nodes, relTypes);

            Assert.Contains("original text", prompt.User);
            Assert.Contains(node.Id.ToString(), prompt.User);
            Assert.Contains("Node A", prompt.User);
            Assert.Contains(relTypeId.ToString(), prompt.User);
            Assert.Contains("causes", prompt.User);
        }

        // ---------- BuildGraphOverviewPrompt ----------

        [Fact]
        public void GraphOverviewPrompt_CurrentlyReturnsEmptyPrompt()
        {
            var document = TestData.NewDocument(Guid.NewGuid());
            var node = TestData.NewNode();
            document.Nodes[node.Id] = node;

            var prompt = _builder.BuildGraphOverviewPrompt(document, new GraphView(document));

            // Pinned to current (unfinished) behavior — update when implemented.
            Assert.Equal("", prompt.System);
            Assert.Equal("", prompt.User);
        }
    }
}
