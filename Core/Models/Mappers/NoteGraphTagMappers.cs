using graphnotelm.Core.Models.DTOs;
using Riok.Mapperly.Abstractions;

namespace graphnotelm.Core.Models.Mappers
{
    [Mapper]
    public static partial class NoteGraphTagMapper
    {
        [MapProperty(nameof(CreateTagRequest.TagName), nameof(TagDefinition.Name))]
        [MapProperty(nameof(CreateTagRequest.TagColor), nameof(TagDefinition.Color))]
        public static partial TagDefinition ToTagDefinition(this CreateTagRequest request);

        [MapperIgnoreSource(nameof(EditTagRequest.Id))]
        [MapProperty(nameof(EditTagRequest.TagName), nameof(TagDefinition.Name))]
        [MapProperty(nameof(EditTagRequest.TagColor), nameof(TagDefinition.Color))]
        public static partial void ApplyTo(this EditTagRequest request, TagDefinition tag);

        [MapperIgnoreSource(nameof(TagDefinition.Color))]
        [MapProperty(nameof(TagDefinition.Name), nameof(CreateTagResponse.TagName))]
        public static partial CreateTagResponse ToCreateTagResponse(this TagDefinition tag);

        [MapperIgnoreSource(nameof(TagDefinition.Color))]
        [MapProperty(nameof(TagDefinition.Name), nameof(DeleteTagResponse.TagName))]
        public static partial DeleteTagResponse ToDeleteTagResponse(this TagDefinition tag);
    }
}
