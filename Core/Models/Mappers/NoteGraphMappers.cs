using graphnotelm.Core.Models.DTOs;
using Riok.Mapperly.Abstractions;

namespace graphnotelm.Core.Models.Mappers
{
    [Mapper]
    public static partial class NoteGraphMapper
    {
        [MapProperty(nameof(CreateGraphRequest.isPublic), nameof(NoteGraphMetadata.IsPublic))]
        [MapProperty(nameof(CreateGraphRequest.isDeleted), nameof(NoteGraphMetadata.IsDeleted))]
        [MapperIgnoreTarget(nameof(NoteGraphMetadata.CreatedAt))]
        [MapperIgnoreTarget(nameof(NoteGraphMetadata.UpdatedAt))]
        public static partial NoteGraphMetadata ToNoteGraphMetadata(this CreateGraphRequest request, Guid id, Guid userId);

        [MapperIgnoreSource(nameof(NoteGraphDocumentREADONLY.Tags))]
        [MapperIgnoreSource(nameof(NoteGraphDocumentREADONLY.Folders))]
        [MapperIgnoreSource(nameof(NoteGraphDocumentREADONLY.Relationships))]
        [MapperIgnoreSource(nameof(NoteGraphDocumentREADONLY.Nodes))]
        [MapperIgnoreTarget(nameof(NoteGraphMetadata.Description))]
        [MapperIgnoreTarget(nameof(NoteGraphMetadata.IsPublic))]
        [MapperIgnoreTarget(nameof(NoteGraphMetadata.IsDeleted))]
        [MapperIgnoreTarget(nameof(NoteGraphMetadata.CreatedAt))]
        [MapperIgnoreTarget(nameof(NoteGraphMetadata.UpdatedAt))]
        public static partial NoteGraphMetadata ToNoteGraphMetadata(this NoteGraphDocumentREADONLY document, Guid id, Guid userId);

        // Nodes are persisted separately per node, never inside the document.
        [MapperIgnoreSource(nameof(NoteGraphDocumentREADONLY.Name))]
        [MapperIgnoreSource(nameof(NoteGraphDocumentREADONLY.Nodes))]
        [MapperIgnoreTarget(nameof(NoteGraphDocument.Context))]
        [MapperIgnoreTarget(nameof(NoteGraphDocument.Nodes))]
        public static partial NoteGraphDocument ToNoteGraphDocument(this NoteGraphDocumentREADONLY document, Guid id, Guid userId);

        [MapperIgnoreSource(nameof(NoteGraphMetadata.UserId))]
        [MapperIgnoreSource(nameof(NoteGraphMetadata.IsPublic))]
        [MapperIgnoreSource(nameof(NoteGraphMetadata.IsDeleted))]
        [MapperIgnoreSource(nameof(NoteGraphMetadata.CreatedAt))]
        [MapperIgnoreSource(nameof(NoteGraphMetadata.UpdatedAt))]
        public static partial EditGraphMetadataResponse ToEditGraphMetadataResponse(this NoteGraphMetadata metadata);

        [MapperIgnoreSource(nameof(NoteGraphMetadata.UserId))]
        [MapperIgnoreSource(nameof(NoteGraphMetadata.Name))]
        [MapperIgnoreSource(nameof(NoteGraphMetadata.Description))]
        [MapperIgnoreSource(nameof(NoteGraphMetadata.IsPublic))]
        [MapperIgnoreSource(nameof(NoteGraphMetadata.CreatedAt))]
        [MapperIgnoreSource(nameof(NoteGraphMetadata.UpdatedAt))]
        [MapProperty(nameof(NoteGraphMetadata.Id), nameof(DeleteGraphResponse.id))]
        [MapProperty(nameof(NoteGraphMetadata.IsDeleted), nameof(DeleteGraphResponse.isDeleted))]
        public static partial DeleteGraphResponse ToDeleteGraphResponse(this NoteGraphMetadata metadata);

        [MapperIgnoreSource(nameof(NoteGraphDocument.UserId))]
        [MapProperty("Context.SystemPrompt", nameof(GetGraphSkeletonResponse.SystemPrompt))]
        public static partial GetGraphSkeletonResponse ToGetGraphSkeletonResponse(this NoteGraphDocument document);

        [MapperIgnoreSource(nameof(NoteNode.Note))]
        public static partial NodeSkeleton ToNodeSkeleton(this NoteNode node);

        [MapperIgnoreSource(nameof(NoteNodeMetadata.LLMMetadata))]
        public static partial NodeSkeletonMetadata ToNodeSkeletonMetadata(this NoteNodeMetadata metadata);

        public static GetGraphListResponse ToGetGraphListResponse(this List<NoteGraphMetadata> graphList)
            => new() { GraphList = graphList };
    }
}
