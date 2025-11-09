using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace Dtos
{
    [SwaggerSchema("Inventory item payload returned by the API")]
    public class InventoryItemDto
    {
        [SwaggerSchema("Database identifier for the inventory item")]
        public int ItemId { get; set; }

        [Required]
        [SwaggerSchema("Name of the inventory item")]
        public required string Name { get; set; }

        [Required]
        [SwaggerSchema("Quantity available for the inventory item")]
        public required int Quantity { get; set; }

        [Required]
        [SwaggerSchema("Location of the item in the warehouse")]
        public required string Location { get; set; }
    }
}
