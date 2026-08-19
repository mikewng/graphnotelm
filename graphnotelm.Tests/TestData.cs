using graphnotelm.Core.Models;

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
    }
}
