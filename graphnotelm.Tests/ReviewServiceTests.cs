using graphnotelm.Core.Models;
using graphnotelm.Core.Models.DTOs;
using graphnotelm.Core.Services;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Core.Utils;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;
using Moq;

namespace graphnotelm.Tests
{
    public class ReviewServiceTests
    {
        private readonly Guid _userId = Guid.NewGuid();
        private readonly NoteGraphDocument _document;
        private readonly List<Flashcard> _cards = new();

        private readonly Mock<INoteGraphAccessService> _accessMock = new();
        private readonly Mock<INoteNodeRepository> _nodeRepoMock = new();
        private readonly Mock<IFlashcardRepository> _cardRepoMock = new();
        private readonly Mock<IReviewLogRepository> _logRepoMock = new();
        private readonly FixedTimeProvider _time = new();

        private readonly ReviewService _service;

        public ReviewServiceTests()
        {
            _document = TestData.NewDocument(_userId);
            _service = new ReviewService(_accessMock.Object, _nodeRepoMock.Object, _cardRepoMock.Object, _logRepoMock.Object, _time);

            _accessMock.Setup(a => a.GetAuthorizedSkeletonDocumentAsync(_document.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphDocument>.Ok(_document));
            _accessMock.Setup(a => a.GetAuthorizedMetadataAsync(_document.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphMetadata>.Ok(TestData.NewMetadata(_userId, _document.Id)));
            _nodeRepoMock.Setup(r => r.GetByIdAsync(_document.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, Guid nodeId, CancellationToken _) => _document.Nodes.GetValueOrDefault(nodeId));
            _cardRepoMock.Setup(r => r.GetByGraphAsync(_document.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _cards.ToList());
            _cardRepoMock.Setup(r => r.GetByIdAsync(_document.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, Guid cardId, CancellationToken _) => _cards.FirstOrDefault(c => c.Id == cardId));
        }

        private NoteNode AddNode(string title, float selfRating = 0, DateTime? dueAt = null)
        {
            var node = TestData.NewNode(title);
            node.Metadata.UserConfidenceRate = selfRating;
            if (dueAt is { } due)
                node.Metadata.Memory = new MemoryState { Stability = 5, Difficulty = 5, LastReviewedAt = due.AddDays(-5), DueAt = due, Reviews = 1 };
            _document.Nodes[node.Id] = node;
            return node;
        }

        private Flashcard AddCard(NoteNode node, string front = "Q")
        {
            var card = new Flashcard { Id = Guid.NewGuid(), GraphId = _document.Id, NodeId = node.Id, Front = front, Back = "A" };
            _cards.Add(card);
            return card;
        }

        private Task<Result<ReviewQueueResponse>> Queue(int newLimit = FlashcardLimits.DefaultNewPerSession)
            => _service.GetQueue(_document.Id, new ReviewQueueRequest { NewLimit = newLimit }, CancellationToken.None);

        // ---------- Queue ----------

        [Fact]
        public async Task GetQueue_DueNotesComeFirstMostOverdueFirst_ThenNewNotesWithCards()
        {
            var due = AddNode("Due", dueAt: _time.Now.AddDays(-2));
            var overdue = AddNode("Overdue", dueAt: _time.Now.AddDays(-5));
            AddNode("NotYet", dueAt: _time.Now.AddDays(1));
            var ratedHigh = AddNode("RatedHigh", selfRating: 6);
            var ratedLow = AddNode("RatedLow", selfRating: 2);
            AddNode("NoCards");
            AddCard(ratedHigh);
            AddCard(ratedLow, "Low card");

            var result = await Queue();

            Assert.True(result.Success);
            Assert.Equal(new[] { overdue.Id, due.Id, ratedLow.Id, ratedHigh.Id }, result.Value!.Items.Select(i => i.NodeId));
            Assert.Equal((2, 2), (result.Value.DueCount, result.Value.NewCount));
            var low = result.Value.Items[2];
            Assert.True(low.IsNew);
            Assert.Null(low.DueAt);
            Assert.Equal("Low card", Assert.Single(low.Cards).Front);
        }

        [Fact]
        public async Task GetQueue_LimitsNewNotes()
        {
            foreach (var rating in new[] { 5f, 1f, 3f })
                AddCard(AddNode($"Rated {rating}", selfRating: rating));

            var result = await Queue(newLimit: 1);

            Assert.Equal("Rated 1", Assert.Single(result.Value!.Items).Title);
            Assert.Equal(1, result.Value.NewCount);
        }

        [Fact]
        public async Task GetQueue_DueNoteWithoutCards_IsReviewedFromItsOwnText()
        {
            var due = AddNode("Self graded", dueAt: _time.Now.AddMinutes(-1));

            var item = Assert.Single((await Queue()).Value!.Items);

            Assert.Equal(due.Id, item.NodeId);
            Assert.False(item.IsNew);
            Assert.Empty(item.Cards);
            Assert.Equal(MemoryModel.Confidence(due.Metadata, _time.Now), item.Confidence);
        }

        [Fact]
        public async Task GetSummary_ReturnsTheQueueCounts()
        {
            AddNode("Due", dueAt: _time.Now.AddDays(-1));
            AddCard(AddNode("New"));

            var result = await _service.GetSummary(_document.Id, new ReviewQueueRequest(), CancellationToken.None);

            Assert.Equal((1, 1), (result.Value!.DueCount, result.Value.NewCount));
        }

        [Fact]
        public async Task GetQueue_AccessDenied_Fails()
        {
            var otherGraph = Guid.NewGuid();
            _accessMock.Setup(a => a.GetAuthorizedSkeletonDocumentAsync(otherGraph, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphDocument>.Fail("Access denied."));

            var result = await _service.GetQueue(otherGraph, new ReviewQueueRequest(), CancellationToken.None);

            Assert.False(result.Success);
        }

        // ---------- Submit ----------

        private Task<Result<SubmitReviewResponse>> Submit(NoteNode node, ReviewGrade grade, Guid? cardId = null)
            => _service.SubmitReview(new SubmitReviewRequest { Grade = grade, CardId = cardId }, _document.Id, node.Id, CancellationToken.None);

        [Fact]
        public async Task SubmitReview_FirstReview_StoresMemoryLogsItAndReportsConfidence()
        {
            var node = AddNode("New", selfRating: 1);
            var card = AddCard(node);
            ReviewLogEntry? logged = null;
            _logRepoMock.Setup(r => r.AddAsync(It.IsAny<ReviewLogEntry>(), It.IsAny<CancellationToken>()))
                .Callback<ReviewLogEntry, CancellationToken>((e, _) => logged = e)
                .Returns(Task.CompletedTask);

            var result = await Submit(node, ReviewGrade.Good, card.Id);

            Assert.True(result.Success);
            Assert.Equal(8.3f, result.Value!.Confidence);
            Assert.Equal(_time.Now.AddDays(4), result.Value.Memory.DueAt);
            Assert.Same(result.Value.Memory, node.Metadata.Memory);
            _nodeRepoMock.Verify(r => r.SaveAsync(_document.Id, node), Times.Once);

            Assert.NotNull(logged);
            Assert.Equal((node.Id, card.Id, ReviewGrade.Good), (logged!.NodeId, logged.CardId, logged.Grade));
            Assert.Equal(0, logged.ElapsedDays);
            Assert.Null(logged.Retrievability);
            Assert.Equal(result.Value.Memory.Stability, logged.StabilityAfter);
        }

        [Fact]
        public async Task SubmitReview_LaterReview_LogsElapsedTimeAndRecallChance()
        {
            var node = AddNode("Learned");
            node.Metadata.Memory = MemoryModel.Review(null, ReviewGrade.Good, _time.Now.AddDays(-4));
            ReviewLogEntry? logged = null;
            _logRepoMock.Setup(r => r.AddAsync(It.IsAny<ReviewLogEntry>(), It.IsAny<CancellationToken>()))
                .Callback<ReviewLogEntry, CancellationToken>((e, _) => logged = e)
                .Returns(Task.CompletedTask);

            var result = await Submit(node, ReviewGrade.Again);

            Assert.Equal(1, result.Value!.Memory.Lapses);
            Assert.Equal(4, logged!.ElapsedDays, 6);
            Assert.InRange(logged.Retrievability!.Value, 0.85, 0.95);
            Assert.Null(logged.CardId);
        }

        [Fact]
        public async Task SubmitReview_InvalidGrade_FailsWithoutSaving()
        {
            var node = AddNode("Node");

            var result = await Submit(node, (ReviewGrade)7);

            Assert.False(result.Success);
            _nodeRepoMock.Verify(r => r.SaveAsync(It.IsAny<Guid>(), It.IsAny<NoteNode>()), Times.Never);
        }

        [Fact]
        public async Task SubmitReview_CardFromAnotherNote_Fails()
        {
            var node = AddNode("Node");
            var otherCard = AddCard(AddNode("Other"));

            var result = await Submit(node, ReviewGrade.Good, otherCard.Id);

            Assert.False(result.Success);
            Assert.Equal("Flashcard not found on this note.", result.Error);
            Assert.Null(node.Metadata.Memory);
        }

        [Fact]
        public async Task SubmitReview_UnknownNode_Fails()
        {
            var result = await _service.SubmitReview(new SubmitReviewRequest { Grade = ReviewGrade.Good },
                _document.Id, Guid.NewGuid(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Node not found in graph.", result.Error);
        }

        [Fact]
        public async Task SubmitReview_LogFailure_StillSucceeds()
        {
            var node = AddNode("Node");
            _logRepoMock.Setup(r => r.AddAsync(It.IsAny<ReviewLogEntry>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("disk full"));

            var result = await Submit(node, ReviewGrade.Easy);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task SubmitReview_SaveFailure_FailsAndLogsNothing()
        {
            var node = AddNode("Node");
            _nodeRepoMock.Setup(r => r.SaveAsync(_document.Id, node)).ThrowsAsync(new IOException("locked"));

            var result = await Submit(node, ReviewGrade.Good);

            Assert.False(result.Success);
            Assert.Equal("Failed to save review.", result.Error);
            _logRepoMock.Verify(r => r.AddAsync(It.IsAny<ReviewLogEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
