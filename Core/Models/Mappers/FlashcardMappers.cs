using graphnotelm.Core.Models.DTOs;
using Riok.Mapperly.Abstractions;

namespace graphnotelm.Core.Models.Mappers
{
    [Mapper]
    public static partial class FlashcardMapper
    {
        [MapperIgnoreSource(nameof(Flashcard.GraphId))]
        public static partial FlashcardResult ToFlashcardResult(this Flashcard card);

        [MapperIgnoreSource(nameof(Flashcard.Id))]
        [MapperIgnoreSource(nameof(Flashcard.GraphId))]
        [MapperIgnoreSource(nameof(Flashcard.CreatedAt))]
        [MapperIgnoreSource(nameof(Flashcard.UpdatedAt))]
        public static partial FlashcardDefinition ToFlashcardDefinition(this Flashcard card);
    }
}
