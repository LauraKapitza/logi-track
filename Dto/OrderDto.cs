using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dtos
{
    public class OrderDto
    {
        public int OrderId { get; set; }

        [Required]
        public required string CustomerName { get; set; }

        public DateTime DatePlaced { get; set; }

        // Require at least one item from clients
        [Required]
        [MinLength(1, ErrorMessage = "At least one item is required for an order.")]
        public required List<InventoryItemDto> Items { get; set; }
    }
}
