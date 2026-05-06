using System.ComponentModel.DataAnnotations;

namespace graphnotelm.Core.Models.DTOs
{
    public class CreateGraphRequest
    {
        [Required]
        public string Name { get; set; }
        public bool isPublic { get; set; } = false;
        public bool isDeleted { get; set; } = false;
    }
}
