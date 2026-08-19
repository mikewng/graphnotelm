using graphnotelm.Core.Models.DTOs;
using Riok.Mapperly.Abstractions;

namespace graphnotelm.Core.Models.Mappers
{
    [Mapper]
    public static partial class NoteGraphRelationshipMapper
    {
        [MapProperty(nameof(CreateRelationshipRequest.Type), nameof(RelationshipDefinition.Name))]
        public static partial RelationshipDefinition ToRelationshipDefinition(this CreateRelationshipRequest request);

        [MapperIgnoreSource(nameof(EditRelationshipRequest.Id))]
        [MapProperty(nameof(EditRelationshipRequest.Type), nameof(RelationshipDefinition.Name))]
        public static partial void ApplyTo(this EditRelationshipRequest request, RelationshipDefinition relationship);
    }
}
