using graphnotelm.Core.Models;
using graphnotelm.Core.Utils;

namespace graphnotelm.Tests
{
    public class MemoryModelTests
    {
        private static readonly DateTime Start = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private static MemoryState Learned(ReviewGrade grade = ReviewGrade.Good)
            => MemoryModel.Review(null, grade, Start);

        [Fact]
        public void Retrievability_IsNinetyPercentWhenElapsedEqualsStability()
        {
            Assert.Equal(0.9, MemoryModel.Retrievability(5, 5), 6);
            Assert.Equal(1.0, MemoryModel.Retrievability(0, 5), 6);
        }

        [Fact]
        public void Retrievability_FallsOverTime()
        {
            Assert.True(MemoryModel.Retrievability(30, 5) < MemoryModel.Retrievability(10, 5));
        }

        [Fact]
        public void FirstReview_Good_SchedulesFourDaysOut()
        {
            var memory = Learned(ReviewGrade.Good);

            Assert.Equal(3.7145, memory.Stability, 4);
            Assert.Equal(5.1618, memory.Difficulty, 4);
            Assert.Equal(Start.AddDays(4), memory.DueAt);
            Assert.Equal(Start, memory.LastReviewedAt);
            Assert.Equal(1, memory.Reviews);
            Assert.Equal(0, memory.Lapses);
        }

        [Fact]
        public void FirstReview_Again_ComesBackInTheSameSessionWithoutCountingALapse()
        {
            var memory = Learned(ReviewGrade.Again);

            Assert.Equal(Start + MemoryModel.RelearnDelay, memory.DueAt);
            Assert.Equal(0, memory.Lapses);
        }

        [Fact]
        public void FirstReview_HigherGrades_GiveMoreStabilityAndLessDifficulty()
        {
            var grades = new[] { ReviewGrade.Again, ReviewGrade.Hard, ReviewGrade.Good, ReviewGrade.Easy };
            var states = grades.Select(Learned).ToList();

            Assert.Equal(states.OrderBy(s => s.Stability), states);
            Assert.Equal(states.OrderByDescending(s => s.Difficulty), states);
        }

        [Fact]
        public void LaterReview_PassingWhenDue_GrowsStability()
        {
            var learned = Learned();

            var next = MemoryModel.Review(learned, ReviewGrade.Good, learned.DueAt);

            Assert.True(next.Stability > learned.Stability * 2);
            Assert.True(next.DueAt > learned.DueAt.AddDays(7));
            Assert.Equal(2, next.Reviews);
        }

        [Fact]
        public void LaterReview_PassingAgainTheSameDay_BarelyChangesStability()
        {
            var learned = Learned();

            var next = MemoryModel.Review(learned, ReviewGrade.Good, Start.AddMinutes(5));

            Assert.Equal(learned.Stability, next.Stability, 1);
        }

        [Fact]
        public void LaterReview_Again_DropsStabilityRaisesDifficultyAndCountsALapse()
        {
            var learned = Learned();

            var next = MemoryModel.Review(learned, ReviewGrade.Again, learned.DueAt);

            Assert.True(next.Stability < learned.Stability);
            Assert.True(next.Difficulty > learned.Difficulty);
            Assert.Equal(1, next.Lapses);
            Assert.Equal(learned.DueAt + MemoryModel.RelearnDelay, next.DueAt);
        }

        [Fact]
        public void LaterReview_HardGrowsLessThanGoodWhichGrowsLessThanEasy()
        {
            var learned = Learned();

            double After(ReviewGrade grade) => MemoryModel.Review(learned, grade, learned.DueAt).Stability;

            Assert.True(After(ReviewGrade.Hard) < After(ReviewGrade.Good));
            Assert.True(After(ReviewGrade.Good) < After(ReviewGrade.Easy));
        }

        [Fact]
        public void Difficulty_StaysWithinOneToTen()
        {
            var failing = Learned(ReviewGrade.Again);
            var acing = Learned(ReviewGrade.Easy);
            for (int i = 1; i <= 50; i++)
            {
                failing = MemoryModel.Review(failing, ReviewGrade.Again, Start.AddDays(i));
                acing = MemoryModel.Review(acing, ReviewGrade.Easy, acing.DueAt);
            }

            Assert.InRange(failing.Difficulty, 1, 10);
            Assert.InRange(acing.Difficulty, 1, 10);
        }

        [Fact]
        public void IntervalDays_MatchesStabilityAtNinetyPercentRetention_AndIsAtLeastADay()
        {
            Assert.Equal(12, MemoryModel.IntervalDays(12.2));
            Assert.Equal(1, MemoryModel.IntervalDays(0.3));
        }

        [Fact]
        public void Confidence_WithoutReviews_IsTheSelfRating()
        {
            var metadata = new NoteNodeMetadata { UserConfidenceRate = 7 };

            Assert.Equal(7f, MemoryModel.Confidence(metadata, Start));
        }

        [Fact]
        public void Confidence_AfterReview_ReflectsTheGradeAndIgnoresTheSelfRating()
        {
            float After(ReviewGrade grade) =>
                MemoryModel.Confidence(new NoteNodeMetadata { UserConfidenceRate = 10, Memory = Learned(grade) }, Start);

            Assert.Equal(4.8f, After(ReviewGrade.Again));
            Assert.Equal(6.8f, After(ReviewGrade.Hard));
            Assert.Equal(8.3f, After(ReviewGrade.Good));
            Assert.Equal(9.5f, After(ReviewGrade.Easy));
        }

        [Fact]
        public void Confidence_FadesWithTime()
        {
            var metadata = new NoteNodeMetadata { Memory = Learned() };

            var fresh = MemoryModel.Confidence(metadata, Start);
            var month = MemoryModel.Confidence(metadata, Start.AddDays(30));
            var year = MemoryModel.Confidence(metadata, Start.AddDays(365));

            Assert.True(fresh > month);
            Assert.True(month > year);
            Assert.InRange(year, 0f, 3f);
        }
    }
}
