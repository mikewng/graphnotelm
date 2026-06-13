using graphnotelm.Core.Models;

namespace graphnotelm.Infrastructure.Repository.Contracts
{
    public interface INoteNodeRepository
    {
        public Task<NoteNode?> GetByIdAsync(Guid noteGraphId, Guid noteNodeId, CancellationToken ct = default);
        public Task<List<NoteNode>> GetAllByGraphIdAsync(Guid noteGraphId, CancellationToken ct = default);
        public Task<List<NoteNode>> SearchAsync(Guid noteGraphId, string query, CancellationToken ct = default);
        public Task SaveAsync(Guid noteGraphId, NoteNode node);
        public Task SaveManyAsync(Guid noteGraphId, IEnumerable<NoteNode> nodes);
        public Task DeleteAsync(Guid noteGraphId, Guid nodeId);
    }
}
