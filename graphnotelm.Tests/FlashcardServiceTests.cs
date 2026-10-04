using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;
using Moq;

namespace graphnotelm.Tests
{
    public class FlashcardServiceTests
    {
        private readonly Guid _graphId = Guid.NewGuid();
        private readonly Guid _nodeId = Guid.NewGuid();

        private readonly Mock<INoteGraphAccessService> _accessMock = new();
        private readonly Mock<INoteNodeRepository> _nodeRepoMock = new();
        private readonly Mock<IFlashcardRepository> _cardRepoMock = new();
        private readonly FixedTimeProvider _time = new();

        private readonly FlashcardService _service;

        public FlashcardServiceTests()
        {
            _service = new FlashcardService(_accessMock.Object, _nodeRepoMock.Object, _cardRepoMock.Object, _time);
            _accessMock.Setup(a => a.GetAuthorizedMetadataAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphMetadata>.Ok(TestData.NewMetadata(Guid.NewGuid(), _graphId)));
            _nodeRepoMock.Setup(r => r.ExistsAsync(_graphId, _nodeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        private Flashcard StoreCard(Guid? nodeId = null)
        {
            var card = new Flashcard
            {
                Id = Guid.NewGuid(),
                GraphId = _graphId,
                NodeId = nodeId ?? _nodeId,
                Front = "Old front",
                Back = "Old back",
                CreatedAt = _time.Now.AddDays(-3),
                UpdatedAt = _time.Now.AddDays(-3)
            };
            _cardRepoMock.Setup(r => r.GetByIdAsync(_graphId, card.Id, It.IsAny<CancellationToken>())).ReturnsAsync(card);
            return card;
        }

        [Fact]
        public async Task Create_TrimsAndSavesTheCard()
        {
            Flashcard? saved = null;
            _cardRepoMock.Setup(r => r.SaveAsync(It.IsAny<Flashcard>(), It.IsAny<CancellationToken>()))
                .Callback<Flashcard, CancellationToken>((c, _) => saved = c)
                .Returns(Task.CompletedTask);

            var result = await _service.CreateFlashcard(
                new CreateFlashcardRequest { Front = "  What is a pod?  ", Back = " The smallest deployable unit. " },
                _graphId, _nodeId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(saved);
            Assert.Equal((_graphId, _nodeId), (saved!.GraphId, saved.NodeId));
            Assert.Equal("What is a pod?", result.Value!.Front);
            Assert.Equal("The smallest deployable unit.", result.Value.Back);
            Assert.Equal(saved.Id, result.Value.Id);
            Assert.Equal(_time.Now, result.Value.CreatedAt);
        }

        [Theory]
        [InlineData("", "back")]
        [InlineData("front", "   ")]
        public async Task Create_BlankSide_FailsWithoutSaving(string front, string back)
        {
            var result = await _service.CreateFlashcard(new CreateFlashcardRequest { Front = front, Back = back },
                _graphId, _nodeId, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("A flashcard needs both a front and a back.", result.Error);
            _cardRepoMock.Verify(r => r.SaveAsync(It.IsAny<Flashcard>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Create_SideTooLong_Fails()
        {
            var result = await _service.CreateFlashcard(
                new CreateFlashcardRequest { Front = new string('x', FlashcardLimits.MaxSideLength + 1), Back = "back" },
                _graphId, _nodeId, CancellationToken.None);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task Create_UnknownNode_Fails()
        {
            var result = await _service.CreateFlashcard(new CreateFlashcardRequest { Front = "f", Back = "b" },
                _graphId, Guid.NewGuid(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Node not found in graph.", result.Error);
        }

        [Fact]
        public async Task Create_AccessDenied_Fails()
        {
            var otherGraph = Guid.NewGuid();
            _accessMock.Setup(a => a.GetAuthorizedMetadataAsync(otherGraph, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphMetadata>.Fail("UserId mismatch. Access to graph metadata denied."));

            var result = await _service.CreateFlashcard(new CreateFlashcardRequest { Front = "f", Back = "b" },
                otherGraph, _nodeId, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("UserId mismatch. Access to graph metadata denied.", result.Error);
        }

        [Fact]
        public async Task GetGraphFlashcards_ListsEveryNoteWithItsCardsAndReviewStatus()
        {
            var document = TestData.NewDocument(Guid.NewGuid(), _graphId);
            NoteNode Add(string title, DateTime? dueAt = null)
            {
                var node = TestData.NewNode(title);
                if (dueAt is { } due)
                    node.Metadata.Memory = new MemoryState { Stability = 5, Difficulty = 5, LastReviewedAt = due.AddDays(-5), DueAt = due, Reviews = 2 };
                document.Nodes[node.Id] = node;
                return node;
            }
            var fresh = Add("b fresh");
            var due = Add("A due", _time.Now.AddDays(-1));
            var later = Add("C later", _time.Now.AddDays(1));
            var bare = Add("D bare");
            _accessMock.Setup(a => a.GetAuthorizedSkeletonDocumentAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphDocument>.Ok(document));
            _cardRepoMock.Setup(r => r.GetByGraphAsync(_graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Flashcard>
                {
                    new() { Id = Guid.NewGuid(), GraphId = _graphId, NodeId = fresh.Id, Front = "Fresh Q", Back = "A" },
                    new() { Id = Guid.NewGuid(), GraphId = _graphId, NodeId = later.Id, Front = "Later Q", Back = "A" },
                    // Left behind by a failed cleanup — its note is gone, so it isn't listed.
                    new() { Id = Guid.NewGuid(), GraphId = _graphId, NodeId = Guid.NewGuid(), Front = "Orphan", Back = "A" },
                });

            var result = await _service.GetGraphFlashcards(_graphId, CancellationToken.None);

            Assert.True(result.Success);
            var notes = result.Value!.Notes;
            Assert.Equal(new[] { due.Id, fresh.Id, later.Id, bare.Id }, notes.Select(n => n.NodeId));
            Assert.Equal(new[] { true, false, false, false }, notes.Select(n => n.IsDue));
            Assert.Equal(new[] { false, true, false, false }, notes.Select(n => n.IsNew));
            Assert.Equal(new[] { 0, 1, 1, 0 }, notes.Select(n => n.Cards.Count));
            Assert.Equal("Fresh Q", notes[1].Cards[0].Front);
            Assert.Null(notes[1].DueAt);
            Assert.Equal((_time.Now.AddDays(1), 2), (notes[2].DueAt!.Value, notes[2].Reviews));
            Assert.Equal(Core.Utils.MemoryModel.Confidence(later.Metadata, _time.Now), notes[2].Confidence);
        }

        [Fact]
        public async Task GetGraphFlashcards_AccessDenied_Fails()
        {
            var otherGraph = Guid.NewGuid();
            _accessMock.Setup(a => a.GetAuthorizedSkeletonDocumentAsync(otherGraph, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphDocument>.Fail("Access denied."));

            var result = await _service.GetGraphFlashcards(otherGraph, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Access denied.", result.Error);
        }

        [Fact]
        public async Task Get_ReturnsTheNotesCards()
        {
            var card = StoreCard();
            _cardRepoMock.Setup(r => r.GetByNodeAsync(_graphId, _nodeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Flashcard> { card });

            var result = await _service.GetFlashcards(_graphId, _nodeId, CancellationToken.None);

            Assert.Equal(card.Id, Assert.Single(result.Value!.Cards).Id);
        }

        [Fact]
        public async Task Edit_UpdatesTextAndTimestampButNotCreation()
        {
            var card = StoreCard();
            var createdAt = card.CreatedAt;

            var result = await _service.EditFlashcard(new EditFlashcardRequest { Front = "New front", Back = "New back" },
                _graphId, _nodeId, card.Id, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(("New front", "New back"), (result.Value!.Front, result.Value.Back));
            Assert.Equal(_time.Now, result.Value.UpdatedAt);
            Assert.Equal(createdAt, result.Value.CreatedAt);
            _cardRepoMock.Verify(r => r.SaveAsync(card, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Edit_CardOnAnotherNote_Fails()
        {
            var card = StoreCard(nodeId: Guid.NewGuid());

            var result = await _service.EditFlashcard(new EditFlashcardRequest { Front = "f", Back = "b" },
                _graphId, _nodeId, card.Id, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Flashcard not found on this note.", result.Error);
            _cardRepoMock.Verify(r => r.SaveAsync(It.IsAny<Flashcard>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Delete_RemovesTheCard()
        {
            var card = StoreCard();

            var result = await _service.DeleteFlashcard(_graphId, _nodeId, card.Id, CancellationToken.None);

            Assert.True(result.Success);
            _cardRepoMock.Verify(r => r.DeleteAsync(_graphId, card.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Delete_UnknownCard_Fails()
        {
            var result = await _service.DeleteFlashcard(_graphId, _nodeId, Guid.NewGuid(), CancellationToken.None);

            Assert.False(result.Success);
            _cardRepoMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
