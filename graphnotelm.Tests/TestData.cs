using graphnotelm.Core.Models;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure.Repository.Contracts;
using graphnotelm.Utils;
using Moq;

namespace graphnotelm.Tests
{
    /// <summary>
    /// Shared builders for graph fixtures used across service test files.
    /// </summary>
    internal static class TestData
    {
        public static NoteGraphDocument NewDocument(Guid userId, Guid? graphId = null)
            => new NoteGraphDocument { Id = graphId ?? Guid.NewGuid(), UserId = userId };

        public static NoteNode NewNode(string title = "Node", string note = "note text")
            => new NoteNode { Id = Guid.NewGuid(), Title = title, Note = note };

        public static NoteGraphMetadata NewMetadata(Guid userId, Guid? graphId = null, string name = "My Graph")
            => new NoteGraphMetadata
            {
                Id = graphId ?? Guid.NewGuid(),
                UserId = userId,
                Name = name
            };

        /// <summary>
        /// Wires the access-service and node-repository mocks so every authorized
        /// read (metadata / document / full / skeleton) and every single-node
        /// repository lookup resolves against the given document, mirroring how the
        /// services now fetch individual nodes instead of the whole graph.
        /// </summary>
        public static void WireDocument(
            Mock<INoteGraphAccessService> accessMock,
            Mock<INoteNodeRepository> nodeRepoMock,
            NoteGraphDocument document,
            Guid userId)
        {
            var graphId = document.Id;

            accessMock.Setup(a => a.GetAuthorizedMetadataAsync(graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphMetadata>.Ok(NewMetadata(userId, graphId)));
            accessMock.Setup(a => a.GetAuthorizedGraphDataAsync(graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphDocument>.Ok(document));
            accessMock.Setup(a => a.GetAuthorizedFullDocumentAsync(graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphDocument>.Ok(document));
            accessMock.Setup(a => a.GetAuthorizedSkeletonDocumentAsync(graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NoteGraphDocument>.Ok(document));

            nodeRepoMock.Setup(r => r.GetByIdAsync(graphId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, Guid nodeId, CancellationToken _) =>
                    document.Nodes.TryGetValue(nodeId, out var node) ? node : null);
            nodeRepoMock.Setup(r => r.ExistsAsync(graphId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, Guid nodeId, CancellationToken _) => document.Nodes.ContainsKey(nodeId));
            nodeRepoMock.Setup(r => r.GetAllByGraphIdAsync(graphId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, CancellationToken _) => document.Nodes.Values.ToList());
            nodeRepoMock.Setup(r => r.GetNodesReferencingIdAsync(graphId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, Guid refId, CancellationToken _) => document.Nodes.Values
                    .Where(n => n.Tags.Contains(refId)
                        || n.Relationships.Any(rel => rel.TargetNodeId == refId || rel.RelationshipId == refId))
                    .ToList());
            nodeRepoMock.Setup(r => r.GetNodesByFolderAsync(graphId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, Guid folderId, CancellationToken _) => document.Nodes.Values
                    .Where(n => n.FolderId == folderId)
                    .ToList());
        }
    }
}
