using graphnotelm.Core.Models.DTOs;
using Riok.Mapperly.Abstractions;

namespace graphnotelm.Core.Models.Mappers
{
    [Mapper]
    public static partial class NoteNodeMapper
    {
        // Only Title and Note are taken from the request at creation time; metadata,
        // relationships, and tags are not persisted on create (matches service behavior).
        [MapperIgnoreSource(nameof(CreateNodeRequest.Metadata))]
        [MapperIgnoreSource(nameof(CreateNodeRequest.Relationships))]
        [MapperIgnoreSource(nameof(CreateNodeRequest.Tags))]
        [MapperIgnoreTarget(nameof(NoteNode.Metadata))]
        [MapperIgnoreTarget(nameof(NoteNode.Relationships))]
        [MapperIgnoreTarget(nameof(NoteNode.Tags))]
        [MapperIgnoreTarget(nameof(NoteNode.FolderId))]
        public static partial NoteNode ToNoteNode(this CreateNodeRequest request, Guid id);

        public static partial GetNodeResponse ToGetNodeResponse(this NoteNode node);

        [MapperIgnoreTarget(nameof(NoteNode.Id))]
        [MapperIgnoreTarget(nameof(NoteNode.Metadata))]
        [MapperIgnoreTarget(nameof(NoteNode.FolderId))]
        public static partial void ApplyTo(this EditNodeRequest request, NoteNode node);

        public static void ApplyTo(this SaveNodeContentRequest request, NoteNode node)
        {
            if (request.Title is not null)
                node.Title = request.Title;
            if (request.Note is not null)
                node.Note = request.Note;
        }

        public static void ApplyTo(this EditNodeMetadataRequest request, NoteNodeMetadata metadata)
        {
            if (request.UserConfidenceRate.HasValue)
                metadata.UserConfidenceRate = request.UserConfidenceRate.Value;
            if (request.LLMMetadata is not null)
                metadata.LLMMetadata = request.LLMMetadata;
        }

        [MapperIgnoreSource(nameof(NoteNode.Note))]
        [MapperIgnoreSource(nameof(NoteNode.Metadata))]
        [MapperIgnoreSource(nameof(NoteNode.Relationships))]
        [MapperIgnoreSource(nameof(NoteNode.Tags))]
        [MapperIgnoreSource(nameof(NoteNode.FolderId))]
        public static partial CreateNodeResponse ToCreateNodeResponse(this NoteNode node);

        [MapperIgnoreSource(nameof(NoteNode.Note))]
        [MapperIgnoreSource(nameof(NoteNode.Metadata))]
        [MapperIgnoreSource(nameof(NoteNode.Relationships))]
        [MapperIgnoreSource(nameof(NoteNode.Tags))]
        [MapperIgnoreSource(nameof(NoteNode.FolderId))]
        public static partial PinnedNodeResult ToPinnedNodeResult(this NoteNode node);

        [MapperIgnoreSource(nameof(NoteNode.Note))]
        [MapperIgnoreSource(nameof(NoteNode.Metadata))]
        [MapperIgnoreSource(nameof(NoteNode.Relationships))]
        [MapperIgnoreSource(nameof(NoteNode.Tags))]
        [MapperIgnoreSource(nameof(NoteNode.FolderId))]
        public static partial NodeSearchResult ToNodeSearchResult(this NoteNode node, string snippet, bool matchedTitle, bool matchedNote);

        [MapProperty(nameof(NoteNode.Id), nameof(EditNodeMetadataResponse.NodeId))]
        [MapperIgnoreSource(nameof(NoteNode.Title))]
        [MapperIgnoreSource(nameof(NoteNode.Note))]
        [MapperIgnoreSource(nameof(NoteNode.Relationships))]
        [MapperIgnoreSource(nameof(NoteNode.Tags))]
        [MapperIgnoreSource(nameof(NoteNode.FolderId))]
        public static partial EditNodeMetadataResponse ToEditNodeMetadataResponse(this NoteNode node);
    }
}
