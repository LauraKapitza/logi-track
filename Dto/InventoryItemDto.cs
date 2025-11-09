using System.ComponentModel.DataAnnotations;

namespace Dtos
{
    public class InventoryItemDto
    {
        public int ItemId { get; set; }

        [Required]
        public required string Name { get; set; }

        [Required]
        public required int Quantity { get; set; }

        [Required]
        public required string Location { get; set; }
    }
}
