namespace graphnotelm.Core.Models
{
    // FSRS grades. The numeric values feed the scheduler's formulas.
    public enum ReviewGrade
    {
        Again = 1,
        Hard = 2,
        Good = 3,
        Easy = 4,
    }

    /// <summary>
    /// How well a note is remembered, updated on every review. A note without one
    /// has never been reviewed, and its confidence is the user's self-rating.
    /// </summary>
    public class MemoryState
    {
        // Days until the chance of recalling the note falls to 90%.
        public double Stability { get; set; }
        // 1 (easy) to 10 (hard); controls how fast stability grows.
        public double Difficulty { get; set; }
        public DateTime LastReviewedAt { get; set; }
        public DateTime DueAt { get; set; }
        public int Reviews { get; set; }
        // Times the note was forgotten after being learned.
        public int Lapses { get; set; }
    }

    public class Flashcard
    {
        public Guid Id { get; set; }
        public Guid GraphId { get; set; }
        public Guid NodeId { get; set; }
        public string Front { get; set; } = string.Empty;
        public string Back { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // A flashcard as it appears in an exported graph — ids are reassigned on import.
    public class FlashcardDefinition
    {
        public Guid NodeId { get; set; }
        public string Front { get; set; } = string.Empty;
        public string Back { get; set; } = string.Empty;
    }

    public class ReviewLogEntry
    {
        public Guid Id { get; set; }
        public Guid GraphId { get; set; }
        public Guid NodeId { get; set; }
        // Null when the note was reviewed without a card.
        public Guid? CardId { get; set; }
        public ReviewGrade Grade { get; set; }
        public DateTime ReviewedAt { get; set; }
        public double ElapsedDays { get; set; }
        // Chance of recall when the review happened; null for a note's first review.
        public double? Retrievability { get; set; }
        public double StabilityAfter { get; set; }
        public double DifficultyAfter { get; set; }
    }
}
