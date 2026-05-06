using graphnotelm.Core.Models;

namespace graphnotelm.Infrastructure.Repository.Contracts
{
    public interface INoteGraphRepositoryPROD
    {
        Task<NoteNode?> GetNodeAsync(Guid noteGraphId, Guid nodeId);
        Task<List<NoteNode>> GetAllNodesAsync(Guid noteGraphId);
        Task SaveNodeAsync(Guid noteGraphId, NoteNode node);
        Task DeleteNodeAsync(Guid noteGraphId, Guid nodeId);
        Task SaveMetaAsync(NoteGraphMetadata meta);
    }
}
