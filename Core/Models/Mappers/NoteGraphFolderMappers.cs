using graphnotelm.Core.Models.DTOs;
using Riok.Mapperly.Abstractions;

namespace graphnotelm.Core.Models.Mappers
{
    [Mapper]
    public static partial class NoteGraphFolderMapper
    {
        [MapProperty(nameof(CreateFolderRequest.FolderName), nameof(FolderDefinition.Name))]
        [MapProperty(nameof(CreateFolderRequest.FolderColor), nameof(FolderDefinition.Color))]
        public static partial FolderDefinition ToFolderDefinition(this CreateFolderRequest request);

        [MapperIgnoreSource(nameof(EditFolderRequest.Id))]
        [MapProperty(nameof(EditFolderRequest.FolderName), nameof(FolderDefinition.Name))]
        [MapProperty(nameof(EditFolderRequest.FolderColor), nameof(FolderDefinition.Color))]
        public static partial void ApplyTo(this EditFolderRequest request, FolderDefinition folder);

        [MapperIgnoreSource(nameof(FolderDefinition.Color))]
        [MapProperty(nameof(FolderDefinition.Name), nameof(CreateFolderResponse.FolderName))]
        public static partial CreateFolderResponse ToCreateFolderResponse(this FolderDefinition folder, Guid id);

        [MapperIgnoreSource(nameof(FolderDefinition.Color))]
        [MapProperty(nameof(FolderDefinition.Name), nameof(DeleteFolderResponse.FolderName))]
        public static partial DeleteFolderResponse ToDeleteFolderResponse(this FolderDefinition folder);
    }
}
