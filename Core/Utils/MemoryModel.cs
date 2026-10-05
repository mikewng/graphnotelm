using graphnotelm.Core.Models;

namespace graphnotelm.Core.Utils
{
    /// <summary>
    /// Spaced-repetition memory model (FSRS-4.5). Each review updates a note's stability
    /// and difficulty; how likely it is to be recalled then follows a forgetting curve,
    /// so confidence is computed from the time since the last review rather than stored.
    /// </summary>
    public static class MemoryModel
    {
        // FSRS-4.5 default parameters, w0–w16.
        private static readonly double[] W =
        {
            0.4872, 1.4003, 3.7145, 13.8206, 5.1618, 1.2298, 0.8975, 0.031,
            1.6474, 0.1367, 1.0461, 2.1072, 0.0793, 0.3246, 1.587, 0.2272, 2.8755,
        };

        private const double Decay = -0.5;
        // Chosen so that recall chance is exactly 90% when elapsed time equals stability.
        private const double Factor = 19.0 / 81.0;
        private const double MaxIntervalDays = 36500;

        public const double DesiredRetention = 0.9;

        // Confidence is the chance of still remembering a note this far ahead. Measuring
        // right now would read as fully known straight after any review, even a failed one.
        public const double ConfidenceHorizonDays = 7;

        // A forgotten note comes back within the same session rather than tomorrow.
        public static readonly TimeSpan RelearnDelay = TimeSpan.FromMinutes(10);

        public static double Retrievability(double elapsedDays, double stability)
            => Math.Pow(1 + Factor * Math.Max(0, elapsedDays) / stability, Decay);

        public static double Retrievability(MemoryState memory, DateTime at)
            => Retrievability((at - memory.LastReviewedAt).TotalDays, memory.Stability);

        /// <summary>
        /// Confidence on the 0–10 scale: measured from reviews when the note has any,
        /// otherwise the user's self-rating.
        /// </summary>
        public static float Confidence(NoteNodeMetadata metadata, DateTime now)
        {
            if (metadata.Memory is not { } memory)
                return metadata.UserConfidenceRate;

            var elapsed = (now - memory.LastReviewedAt).TotalDays;
            return (float)Math.Round(10 * Retrievability(elapsed + ConfidenceHorizonDays, memory.Stability), 1);
        }

        public static MemoryState Review(MemoryState? prior, ReviewGrade grade, DateTime now)
        {
            int g = (int)grade;
            double stability, difficulty;

            if (prior is null)
            {
                stability = W[g - 1];
                difficulty = InitialDifficulty(g);
            }
            else
            {
                double recall = Retrievability(prior, now);
                // Both stability formulas use the difficulty from before this review.
                stability = grade == ReviewGrade.Again
                    ? Math.Min(ForgetStability(prior.Difficulty, prior.Stability, recall), prior.Stability)
                    : RecallStability(prior.Difficulty, prior.Stability, recall, grade);
                difficulty = NextDifficulty(prior.Difficulty, g);
            }

            return new MemoryState
            {
                Stability = stability,
                Difficulty = difficulty,
                LastReviewedAt = now,
                DueAt = grade == ReviewGrade.Again ? now + RelearnDelay : now.AddDays(IntervalDays(stability)),
                Reviews = (prior?.Reviews ?? 0) + 1,
                Lapses = (prior?.Lapses ?? 0) + (prior is not null && grade == ReviewGrade.Again ? 1 : 0),
            };
        }

        // Whole days until recall chance falls to DesiredRetention — at least one, so a
        // passed note is never asked again the same day.
        public static double IntervalDays(double stability)
        {
            double days = stability / Factor * (Math.Pow(DesiredRetention, 1 / Decay) - 1);
            return Math.Clamp(Math.Round(days), 1, MaxIntervalDays);
        }

        private static double InitialDifficulty(int g)
            => Math.Clamp(W[4] - (g - 3) * W[5], 1, 10);

        // Grades move difficulty, and it drifts back toward the default each review.
        private static double NextDifficulty(double difficulty, int g)
        {
            double next = difficulty - W[6] * (g - 3);
            return Math.Clamp(W[7] * W[4] + (1 - W[7]) * next, 1, 10);
        }

        private static double RecallStability(double difficulty, double stability, double recall, ReviewGrade grade)
        {
            double hardPenalty = grade == ReviewGrade.Hard ? W[15] : 1;
            double easyBonus = grade == ReviewGrade.Easy ? W[16] : 1;
            return stability * (1
                + Math.Exp(W[8])
                * (11 - difficulty)
                * Math.Pow(stability, -W[9])
                * (Math.Exp((1 - recall) * W[10]) - 1)
                * hardPenalty
                * easyBonus);
        }

        private static double ForgetStability(double difficulty, double stability, double recall)
            => W[11]
               * Math.Pow(difficulty, -W[12])
               * (Math.Pow(stability + 1, W[13]) - 1)
               * Math.Exp((1 - recall) * W[14]);
    }
}
