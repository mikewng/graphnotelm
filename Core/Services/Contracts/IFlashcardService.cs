using graphnotelm.Core.Models.DTOs;
using graphnotelm.Utils;

namespace graphnotelm.Core.Services.Contracts
{
    public interface IFlashcardService
    {
        public Task<Result<GetFlashcardsResponse>> GetFlashcards(Guid noteGraphId, Guid noteNodeId, CancellationToken ct);
        public Task<Result<FlashcardResult>> CreateFlashcard(CreateFlashcardRequest request, Guid noteGraphId, Guid noteNodeId, CancellationToken ct);
        public Task<Result<FlashcardResult>> EditFlashcard(EditFlashcardRequest request, Guid noteGraphId, Guid noteNodeId, Guid cardId, CancellationToken ct);
        public Task<Result<DeleteFlashcardResponse>> DeleteFlashcard(Guid noteGraphId, Guid noteNodeId, Guid cardId, CancellationToken ct);
    }
}
