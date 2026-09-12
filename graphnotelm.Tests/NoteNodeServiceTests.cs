using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Core.Utils;
using graphnotelm.Infrastructure.Contracts;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;
using Moq;

namespace graphnotelm.Tests
{
    public class NoteNodeServiceTests
    {
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _graphId = Guid.NewGuid();

        private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
        private readonly Mock<INoteGraphAccessService> _accessMock = new();
        private readonly Mock<INoteGraphRepository> _graphRepoMock = new();
        private readonly Mock<INoteNodeRepository> _nodeRepoMock = new();
        private readonly Mock<ILLMAnalysisService> _llmAnalysisMock = new();
        private readonly Mock<IImageRepository> _imageRepoMock = new();

        private readonly NoteNodeService _service;

        public NoteNodeServiceTests()
        {
            _service = new NoteNodeService(
                _unitOfWorkMock.Object,
                _accessMock.Object,
                _graphRepoMock.Object,
                _nodeRepoMock.Object,
                _llmAnalysisMock.Object,
                _imageRepoMock.Object);
        }

        private NoteGraphDocument AuthorizeFullDocument()
        {
            var document = TestData.NewDocument(_userId, _graphId);
            TestData.WireDocument(_accessMock, _nodeRepoMock, document, _userId);
            return document;
        }

        private void AuthorizeMetadata()
        {
            _accessMock.Setup(a => a.GetAuthorizedMetadataAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphMetadata>.Ok(TestData.NewMetadata(_userId, _graphId)));
        }

        // ---------- GetNodeByIds ----------

        [Fact]
        public async Task GetNode_AccessDenied_Fails()
        {
            _accessMock.Setup(a => a.GetAuthorizedMetadataAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphMetadata>.Fail("denied"));

            var result = await _service.GetNodeByIds(_graphId, Guid.NewGuid(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("denied", result.Error);
        }

        [Fact]
        public async Task GetNode_NotFound_Fails()
        {
            AuthorizeMetadata();
            _nodeRepoMock.Setup(r => r.GetByIdAsync(_graphId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((NoteNode?)null);

            var result = await _service.GetNodeByIds(_graphId, Guid.NewGuid(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Node not found.", result.Error);
        }

        [Fact]
        public async Task GetNode_ReturnsNodeContent()
        {
            AuthorizeMetadata();
            var node = TestData.NewNode("My Node", "body");
            _nodeRepoMock.Setup(r => r.GetByIdAsync(_graphId, node.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(node);

            var result = await _service.GetNodeByIds(_graphId, node.Id, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(node.Id, result.Value!.Id);
            Assert.Equal("My Node", result.Value.Title);
            Assert.Equal("body", result.Value.Note);
        }

        // ---------- GetNodeBatchByIds ----------

        [Fact]
        public async Task GetNodeBatch_SkipsMissingNodes()
        {
            AuthorizeMetadata();
            var found = TestData.NewNode("Found");
            var missingId = Guid.NewGuid();
            _nodeRepoMock.Setup(r => r.GetByIdAsync(_graphId, found.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(found);
            _nodeRepoMock.Setup(r => r.GetByIdAsync(_graphId, missingId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((NoteNode?)null);

            var result = await _service.GetNodeBatchByIds(_graphId, new List<Guid> { found.Id, missingId }, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Single(result.Value!.Nodes);
            Assert.True(result.Value.Nodes.ContainsKey(found.Id));
        }

        // ---------- CreateNodeByGraphId ----------

        [Fact]
        public async Task CreateNode_EmptyTitle_Fails()
        {
            AuthorizeFullDocument();

            var result = await _service.CreateNodeByGraphId(
                new CreateNodeRequest { Title = "" }, _graphId, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Failed to create: Title was empty.", result.Error);
        }

        [Fact]
        public async Task CreateNode_Valid_SavesNode()
        {
            AuthorizeFullDocument();
            NoteNode? saved = null;
            _nodeRepoMock.Setup(r => r.SaveAsync(_graphId, It.IsAny<NoteNode>()))
                .Callback<Guid, NoteNode>((_, n) => saved = n)
                .Returns(Task.CompletedTask);

            var result = await _service.CreateNodeByGraphId(
                new CreateNodeRequest { Title = "New Node", Note = "content" }, _graphId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(saved);
            Assert.Equal("New Node", saved!.Title);
            Assert.Equal("content", saved.Note);
            Assert.Equal(saved.Id, result.Value!.Id);
        }

        // ---------- EditNodeByIds ----------

        [Fact]
        public async Task EditNode_NodeNotInGraph_Fails()
        {
            AuthorizeFullDocument();

            var result = await _service.EditNodeByIds(
                new EditNodeRequest { Title = "t" }, _graphId, Guid.NewGuid(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Node not found in graph.", result.Error);
        }

        [Fact]
        public async Task EditNode_UnknownTag_Fails()
        {
            var document = AuthorizeFullDocument();
            var node = TestData.NewNode();
            document.Nodes[node.Id] = node;

            var result = await _service.EditNodeByIds(
                new EditNodeRequest { Title = "t", Tags = new List<Guid> { Guid.NewGuid() } },
                _graphId, node.Id, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("Tag(s) not found in graph", result.Error);
        }

        [Fact]
        public async Task EditNode_SelfRelationship_Fails()
        {
            var document = AuthorizeFullDocument();
            var node = TestData.NewNode();
            document.Nodes[node.Id] = node;

            var result = await _service.EditNodeByIds(
                new EditNodeRequest
                {
                    Title = "t",
                    Relationships = new List<NodeRelationship>
                    {
                        new NodeRelationship { TargetNodeId = node.Id, RelationshipId = Guid.NewGuid() }
                    }
                },
                _graphId, node.Id, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("A node cannot have a relationship with itself.", result.Error);
        }

        [Fact]
        public async Task EditNode_Valid_UpdatesAndSaves()
        {
            var document = AuthorizeFullDocument();
            var node = TestData.NewNode("Old", "old note");
            var target = TestData.NewNode("Target");
            var tagId = Guid.NewGuid();
            var relTypeId = Guid.NewGuid();
            document.Nodes[node.Id] = node;
            document.Nodes[target.Id] = target;
            document.Tags[tagId] = new TagDefinition { Name = "tag" };
            document.Relationships[relTypeId] = new RelationshipDefinition { Name = "rel" };

            var result = await _service.EditNodeByIds(
                new EditNodeRequest
                {
                    Title = "New",
                    Note = "new note",
                    Tags = new List<Guid> { tagId },
                    Relationships = new List<NodeRelationship>
                    {
                        new NodeRelationship { TargetNodeId = target.Id, RelationshipId = relTypeId }
                    }
                },
                _graphId, node.Id, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("New", node.Title);
            Assert.Equal("new note", node.Note);
            Assert.Single(node.Tags);
            Assert.Single(node.Relationships);
            _nodeRepoMock.Verify(r => r.SaveAsync(_graphId, node), Times.Once);
        }

        // ---------- DeleteNodeByIds ----------

        [Fact]
        public async Task DeleteNode_RemovesDanglingRelationshipsFromOtherNodes()
        {
            var document = AuthorizeFullDocument();
            var toDelete = TestData.NewNode("Delete Me");
            var pointsAtDeleted = TestData.NewNode("Referrer");
            pointsAtDeleted.Relationships.Add(new NodeRelationship { TargetNodeId = toDelete.Id, RelationshipId = Guid.NewGuid() });
            var unrelated = TestData.NewNode("Unrelated");
            document.Nodes[toDelete.Id] = toDelete;
            document.Nodes[pointsAtDeleted.Id] = pointsAtDeleted;
            document.Nodes[unrelated.Id] = unrelated;

            var result = await _service.DeleteNodeByIds(_graphId, toDelete.Id, CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(result.Value!.IsDeleted);
            Assert.Empty(pointsAtDeleted.Relationships);
            _nodeRepoMock.Verify(r => r.DeleteAsync(_graphId, toDelete.Id), Times.Once);
            _nodeRepoMock.Verify(r => r.SaveManyAsync(_graphId,
                It.Is<IEnumerable<NoteNode>>(nodes => nodes.Single() == pointsAtDeleted)), Times.Once);
        }

        [Fact]
        public async Task DeleteNode_NotFound_Fails()
        {
            AuthorizeFullDocument();

            var result = await _service.DeleteNodeByIds(_graphId, Guid.NewGuid(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Node not found in graph.", result.Error);
            _imageRepoMock.Verify(r => r.DeleteByNodeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DeleteNode_DeletesNodeImages()
        {
            var document = AuthorizeFullDocument();
            var node = TestData.NewNode();
            document.Nodes[node.Id] = node;

            var result = await _service.DeleteNodeByIds(_graphId, node.Id, CancellationToken.None);

            Assert.True(result.Success);
            _imageRepoMock.Verify(r => r.DeleteByNodeAsync(_graphId, node.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteNode_ImageCleanupFails_StillSucceeds()
        {
            var document = AuthorizeFullDocument();
            var node = TestData.NewNode();
            document.Nodes[node.Id] = node;
            _imageRepoMock.Setup(r => r.DeleteByNodeAsync(_graphId, node.Id, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("locked"));

            var result = await _service.DeleteNodeByIds(_graphId, node.Id, CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(result.Value!.IsDeleted);
        }

        [Fact]
        public async Task DeleteNode_NodeDeleteFails_DoesNotDeleteImages()
        {
            var document = AuthorizeFullDocument();
            var node = TestData.NewNode();
            document.Nodes[node.Id] = node;
            _nodeRepoMock.Setup(r => r.DeleteAsync(_graphId, node.Id)).ThrowsAsync(new InvalidOperationException());

            var result = await _service.DeleteNodeByIds(_graphId, node.Id, CancellationToken.None);

            Assert.False(result.Success);
            _imageRepoMock.Verify(r => r.DeleteByNodeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ---------- SaveNodeContentAsync ----------

        [Fact]
        public async Task SaveNodeContent_EmptyTitle_Fails()
        {
            var document = AuthorizeFullDocument();
            var node = TestData.NewNode("Keep");
            document.Nodes[node.Id] = node;

            var result = await _service.SaveNodeContentAsync(
                new SaveNodeContentRequest { Title = "" }, _graphId, node.Id, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Title cannot be empty.", result.Error);
            Assert.Equal("Keep", node.Title);
        }

        [Fact]
        public async Task SaveNodeContent_NullFields_LeaveExistingValues()
        {
            var document = AuthorizeFullDocument();
            var node = TestData.NewNode("Keep", "keep note");
            document.Nodes[node.Id] = node;

            var result = await _service.SaveNodeContentAsync(
                new SaveNodeContentRequest { Title = null, Note = "updated note" }, _graphId, node.Id, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("Keep", node.Title);
            Assert.Equal("updated note", node.Note);
            _nodeRepoMock.Verify(r => r.SaveAsync(_graphId, node), Times.Once);
        }

        // ---------- CreateNodeFromPastedContent ----------

        [Fact]
        public async Task CreateNodeFromPaste_EmptyContent_Fails()
        {
            var result = await _service.CreateNodeFromPastedContent(
                new CreateNotePastedRequest { PastedContent = " " }, _graphId, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Pasted content was empty.", result.Error);
        }

        [Fact]
        public async Task CreateNodeFromPaste_UsesExtractedContent()
        {
            var extracted = new CreateNodeRequest
            {
                Title = "Extracted Title",
                Note = "Extracted note",
                Tags = new List<Guid> { Guid.NewGuid() }
            };
            _llmAnalysisMock.Setup(l => l.ExtractNodeFromPasteAsync(_graphId, "pasted text", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<CreateNodeRequest>.Ok(extracted));

            NoteNode? saved = null;
            _nodeRepoMock.Setup(r => r.SaveAsync(_graphId, It.IsAny<NoteNode>()))
                .Callback<Guid, NoteNode>((_, n) => saved = n)
                .Returns(Task.CompletedTask);

            var result = await _service.CreateNodeFromPastedContent(
                new CreateNotePastedRequest { PastedContent = "pasted text" }, _graphId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("Extracted Title", result.Value!.Title);
            Assert.NotNull(saved);
            Assert.Equal(extracted.Tags, saved!.Tags);
        }

        // ---------- SearchNodesByContent ----------

        [Fact]
        public async Task SearchNodes_BuildsSnippetsAndMatchFlags()
        {
            AuthorizeMetadata();
            var titleMatch = TestData.NewNode("Quantum Mechanics", "unrelated body");
            var noteMatch = TestData.NewNode("Other", new string('x', 300) + " quantum stuff " + new string('y', 300));
            _nodeRepoMock.Setup(r => r.SearchAsync(_graphId, "quantum", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<NoteNode> { titleMatch, noteMatch });

            var result = await _service.SearchNodesByContent(_graphId, "quantum", CancellationToken.None);

            Assert.True(result.Success);
            var titleResult = result.Value!.Results.First(r => r.Id == titleMatch.Id);
            Assert.True(titleResult.MatchedTitle);
            Assert.False(titleResult.MatchedNote);
            Assert.Equal(titleMatch.Title, titleResult.Snippet);

            var noteResult = result.Value.Results.First(r => r.Id == noteMatch.Id);
            Assert.True(noteResult.MatchedNote);
            Assert.Contains("quantum", noteResult.Snippet);
            Assert.StartsWith("...", noteResult.Snippet);
            Assert.EndsWith("...", noteResult.Snippet);
        }

        // ---------- Pinning ----------

        [Fact]
        public async Task GetPinnedNodes_ReturnsOnlyPinned()
        {
            AuthorizeMetadata();
            var pinned = TestData.NewNode("Pinned");
            pinned.Metadata.IsPinned = true;
            var unpinned = TestData.NewNode("Unpinned");
            _nodeRepoMock.Setup(r => r.GetAllByGraphIdAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<NoteNode> { pinned, unpinned });

            var result = await _service.GetPinnedNodes(_graphId, CancellationToken.None);

            Assert.True(result.Success);
            var only = Assert.Single(result.Value!.Nodes);
            Assert.Equal(pinned.Id, only.Id);
        }

        [Fact]
        public async Task SetNodePinned_UpdatesAndSaves()
        {
            AuthorizeMetadata();
            var node = TestData.NewNode();
            _nodeRepoMock.Setup(r => r.GetByIdAsync(_graphId, node.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(node);

            var result = await _service.SetNodePinned(_graphId, node.Id, true, CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(node.Metadata.IsPinned);
            _nodeRepoMock.Verify(r => r.SaveAsync(_graphId, node), Times.Once);
        }

        [Fact]
        public async Task SetNodesPinned_SkipsMissingAndAlreadyPinnedNodes()
        {
            AuthorizeMetadata();
            var alreadyPinned = TestData.NewNode("Already");
            alreadyPinned.Metadata.IsPinned = true;
            var toPin = TestData.NewNode("To Pin");
            var missingId = Guid.NewGuid();
            _nodeRepoMock.Setup(r => r.GetAllByGraphIdAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<NoteNode> { alreadyPinned, toPin });

            var result = await _service.SetNodesPinned(
                new SetPinnedManyRequest { NodeIds = new List<Guid> { alreadyPinned.Id, toPin.Id, missingId } },
                _graphId, isPinned: true, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(new List<Guid> { toPin.Id }, result.Value!.UpdatedNodeIds);
            Assert.Contains(alreadyPinned.Id, result.Value.SkippedNodeIds);
            Assert.Contains(missingId, result.Value.SkippedNodeIds);
            _nodeRepoMock.Verify(r => r.SaveManyAsync(_graphId,
                It.Is<IEnumerable<NoteNode>>(nodes => nodes.Single() == toPin)), Times.Once);
        }

        // ---------- EditNodeMetadataByIds ----------

        [Fact]
        public async Task EditNodeMetadata_UpdatesOnlyProvidedFields()
        {
            var document = AuthorizeFullDocument();
            var node = TestData.NewNode();
            node.Metadata.UserConfidenceRate = 0.1f;
            node.Metadata.LLMMetadata = "existing";
            document.Nodes[node.Id] = node;

            var result = await _service.EditNodeMetadataByIds(
                new EditNodeMetadataRequest { UserConfidenceRate = 0.9f, LLMMetadata = null },
                _graphId, node.Id, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(0.9f, node.Metadata.UserConfidenceRate);
            Assert.Equal("existing", node.Metadata.LLMMetadata);
            _nodeRepoMock.Verify(r => r.SaveAsync(_graphId, node), Times.Once);
        }

        // ---------- UploadImageAsync ----------

        private NoteNode AuthorizeWithNode()
        {
            var document = AuthorizeFullDocument();
            var node = TestData.NewNode();
            document.Nodes[node.Id] = node;
            return node;
        }

        private static UploadImageRequest ImageRequest(byte[] bytes, string fileName = "photo.png")
            => new UploadImageRequest { Content = new MemoryStream(bytes), FileName = fileName };

        [Fact]
        public async Task UploadImage_AccessDenied_Fails()
        {
            _accessMock.Setup(a => a.GetAuthorizedMetadataAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphMetadata>.Fail("denied"));

            var result = await _service.UploadImageAsync(ImageRequest(TestData.PngBytes()), _graphId, Guid.NewGuid(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("denied", result.Error);
            _imageRepoMock.Verify(r => r.SaveAsync(It.IsAny<NoteImage>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UploadImage_NodeNotFound_Fails()
        {
            AuthorizeFullDocument();

            var result = await _service.UploadImageAsync(ImageRequest(TestData.PngBytes()), _graphId, Guid.NewGuid(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Node not found in graph.", result.Error);
        }

        [Fact]
        public async Task UploadImage_EmptyFile_Fails()
        {
            var node = AuthorizeWithNode();

            var result = await _service.UploadImageAsync(ImageRequest(Array.Empty<byte>()), _graphId, node.Id, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Image was empty.", result.Error);
        }

        [Fact]
        public async Task UploadImage_OverSizeLimit_Fails()
        {
            var node = AuthorizeWithNode();

            var result = await _service.UploadImageAsync(
                ImageRequest(TestData.PngBytes((int)ImageFormats.MaxUploadBytes + 1)), _graphId, node.Id, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("MB limit", result.Error);
            _imageRepoMock.Verify(r => r.SaveAsync(It.IsAny<NoteImage>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UploadImage_AtSizeLimit_Succeeds()
        {
            var node = AuthorizeWithNode();

            var result = await _service.UploadImageAsync(
                ImageRequest(TestData.PngBytes((int)ImageFormats.MaxUploadBytes)), _graphId, node.Id, CancellationToken.None);

            Assert.True(result.Success);
        }

        [Theory]
        [InlineData("fake.png", "not an image at all")]
        [InlineData("drawing.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")]
        public async Task UploadImage_UnsupportedBytes_FailsRegardlessOfFileName(string fileName, string content)
        {
            var node = AuthorizeWithNode();

            var result = await _service.UploadImageAsync(
                ImageRequest(System.Text.Encoding.UTF8.GetBytes(content), fileName), _graphId, node.Id, CancellationToken.None);

            Assert.False(result.Success);
            Assert.StartsWith("Unsupported image type", result.Error);
            _imageRepoMock.Verify(r => r.SaveAsync(It.IsAny<NoteImage>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UploadImage_ValidPng_SavesFullStreamAndReturnsUrl()
        {
            var node = AuthorizeWithNode();
            var bytes = TestData.PngBytes(128);
            NoteImage? saved = null;
            byte[]? savedBytes = null;
            _imageRepoMock.Setup(r => r.SaveAsync(It.IsAny<NoteImage>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<NoteImage, Stream, CancellationToken>((image, stream, _) =>
                {
                    saved = image;
                    using var copy = new MemoryStream();
                    stream.CopyTo(copy);
                    savedBytes = copy.ToArray();
                })
                .Returns(Task.CompletedTask);

            var result = await _service.UploadImageAsync(ImageRequest(bytes), _graphId, node.Id, CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(saved);
            Assert.Equal(_graphId, saved!.GraphId);
            Assert.Equal(node.Id, saved.NodeId);
            Assert.Equal("image/png", saved.ContentType);
            Assert.Equal(bytes.Length, saved.SizeBytes);
            Assert.Equal("photo.png", saved.OriginalFileName);
            // Format detection reads the header; the repository must still receive every byte.
            Assert.Equal(bytes, savedBytes);
            Assert.Equal(saved.Id, result.Value!.ImageId);
            Assert.Equal($"/NoteGraph/images/{saved.Id}", result.Value.Url);
        }

        [Fact]
        public async Task UploadImage_NonSeekableStream_IsBufferedAndSaved()
        {
            var node = AuthorizeWithNode();
            var bytes = TestData.PngBytes(128);
            byte[]? savedBytes = null;
            _imageRepoMock.Setup(r => r.SaveAsync(It.IsAny<NoteImage>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<NoteImage, Stream, CancellationToken>((_, stream, _) =>
                {
                    using var copy = new MemoryStream();
                    stream.CopyTo(copy);
                    savedBytes = copy.ToArray();
                })
                .Returns(Task.CompletedTask);

            var request = new UploadImageRequest { Content = new NonSeekableStream(new MemoryStream(bytes)), FileName = "photo.png" };
            var result = await _service.UploadImageAsync(request, _graphId, node.Id, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(bytes, savedBytes);
        }

        [Theory]
        [InlineData("../../evil.png", "evil.png")]
        [InlineData("C:\\Windows\\evil.png", "evil.png")]
        public async Task UploadImage_StripsDirectoriesFromFileName(string fileName, string expected)
        {
            var node = AuthorizeWithNode();
            NoteImage? saved = null;
            _imageRepoMock.Setup(r => r.SaveAsync(It.IsAny<NoteImage>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<NoteImage, Stream, CancellationToken>((image, _, _) => saved = image)
                .Returns(Task.CompletedTask);

            await _service.UploadImageAsync(ImageRequest(TestData.PngBytes(), fileName), _graphId, node.Id, CancellationToken.None);

            Assert.Equal(expected, saved!.OriginalFileName);
        }

        [Fact]
        public async Task UploadImage_RepositoryThrows_Fails()
        {
            var node = AuthorizeWithNode();
            _imageRepoMock.Setup(r => r.SaveAsync(It.IsAny<NoteImage>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("disk full"));

            var result = await _service.UploadImageAsync(ImageRequest(TestData.PngBytes()), _graphId, node.Id, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Failed to save image.", result.Error);
        }

        // ---------- GetImageAsync ----------

        [Fact]
        public async Task GetImage_UnknownId_Fails()
        {
            _imageRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((NoteImage?)null);

            var result = await _service.GetImageAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Image not found.", result.Error);
        }

        [Fact]
        public async Task GetImage_StoredBytesMissing_Fails()
        {
            var image = new NoteImage { Id = Guid.NewGuid(), ContentType = "image/png" };
            _imageRepoMock.Setup(r => r.GetByIdAsync(image.Id, It.IsAny<CancellationToken>())).ReturnsAsync(image);
            _imageRepoMock.Setup(r => r.OpenReadAsync(image, It.IsAny<CancellationToken>())).ReturnsAsync((Stream?)null);

            var result = await _service.GetImageAsync(image.Id, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Image not found.", result.Error);
        }

        [Fact]
        public async Task GetImage_ReturnsStreamWithStoredContentType()
        {
            var image = new NoteImage { Id = Guid.NewGuid(), ContentType = "image/webp" };
            var stream = new MemoryStream(TestData.PngBytes());
            _imageRepoMock.Setup(r => r.GetByIdAsync(image.Id, It.IsAny<CancellationToken>())).ReturnsAsync(image);
            _imageRepoMock.Setup(r => r.OpenReadAsync(image, It.IsAny<CancellationToken>())).ReturnsAsync(stream);

            var result = await _service.GetImageAsync(image.Id, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Same(stream, result.Value!.Content);
            Assert.Equal("image/webp", result.Value.ContentType);
        }

        private sealed class NonSeekableStream(Stream inner) : Stream
        {
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }
            public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
