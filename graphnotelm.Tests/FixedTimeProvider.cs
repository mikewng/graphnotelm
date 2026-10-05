namespace graphnotelm.Tests
{
    // A clock tests can set and move, for code that depends on how much time has passed.
    internal sealed class FixedTimeProvider : TimeProvider
    {
        public static readonly DateTime DefaultNow = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

        public DateTime Now { get; set; } = DefaultNow;

        public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
    }
}
