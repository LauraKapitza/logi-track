using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace Dtos
{
    [SwaggerSchema("Order returned by the API")]
    public class OrderDto
    {
        [SwaggerSchema("Database identifier for the order")]
        public int? OrderId { get; set; }

        [Required]
        [SwaggerSchema("Name of the customer who placed the order")]
        public required string CustomerName { get; set; }

        [SwaggerSchema("UTC timestamp when the order was placed")]
        public DateTime DatePlaced { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "At least one item is required for an order.")]
        [SwaggerSchema("Items included in the order; must contain at least one item")]
        public required List<InventoryItemDto> Items { get; set; }
    }
}
