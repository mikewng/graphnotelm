using graphnotelm.Core.Models;

namespace graphnotelm.Infrastructure.Repository.Contracts
{
    public interface IFlashcardRepository
    {
        public Task<List<Flashcard>> GetByNodeAsync(Guid noteGraphId, Guid noteNodeId, CancellationToken ct = default);
        public Task<List<Flashcard>> GetByGraphAsync(Guid noteGraphId, CancellationToken ct = default);
        public Task<Flashcard?> GetByIdAsync(Guid noteGraphId, Guid cardId, CancellationToken ct = default);

        // Inserts the card, or updates its front, back and UpdatedAt if it already exists.
        public Task SaveAsync(Flashcard card, CancellationToken ct = default);
        public Task SaveManyAsync(IEnumerable<Flashcard> cards, CancellationToken ct = default);

        public Task DeleteAsync(Guid noteGraphId, Guid cardId, CancellationToken ct = default);
        public Task DeleteByNodeAsync(Guid noteGraphId, Guid noteNodeId, CancellationToken ct = default);
        public Task DeleteByGraphAsync(Guid noteGraphId, CancellationToken ct = default);
    }
}
