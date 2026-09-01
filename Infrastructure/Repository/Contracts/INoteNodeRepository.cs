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

        // Skeleton nodes carry an empty Note. They exist so read-heavy paths can skip
        // loading note bodies — never pass a skeleton back into SaveAsync/SaveManyAsync,
        // or the stored note is wiped.
        public async Task<List<NoteNode>> GetAllSkeletonsByGraphIdAsync(Guid noteGraphId, CancellationToken ct = default)
            => await GetAllByGraphIdAsync(noteGraphId, ct);

        public async Task<bool> ExistsAsync(Guid noteGraphId, Guid noteNodeId, CancellationToken ct = default)
            => await GetByIdAsync(noteGraphId, noteNodeId, ct) is not null;

        // Full nodes (safe to save) whose tags or relationships reference the given id.
        public async Task<List<NoteNode>> GetNodesReferencingIdAsync(Guid noteGraphId, Guid referencedId, CancellationToken ct = default)
        {
            var all = await GetAllByGraphIdAsync(noteGraphId, ct);
            return all
                .Where(n => n.Tags.Contains(referencedId)
                    || n.Relationships.Any(r => r.TargetNodeId == referencedId || r.RelationshipId == referencedId))
                .ToList();
        }

        // Full nodes (safe to save) assigned to the given folder.
        public async Task<List<NoteNode>> GetNodesByFolderAsync(Guid noteGraphId, Guid folderId, CancellationToken ct = default)
        {
            var all = await GetAllByGraphIdAsync(noteGraphId, ct);
            return all.Where(n => n.FolderId == folderId).ToList();
        }
    }
}
