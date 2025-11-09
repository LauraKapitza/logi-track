using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models
{
    public class Order
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int OrderId { get; set; }

        [Required]
        public required string CustomerName { get; set; }

        public DateTime DatePlaced { get; set; }

        // keep the concrete List type for EF navigation but mark as required for compile-time checks.
        [Required]
        public required List<InventoryItem> Items { get; set; }

        public Order()
        {
            DatePlaced = DateTime.Now;
        }

        public Order(int orderId, string customerName, List<InventoryItem> items)
        {
            OrderId = orderId;
            CustomerName = customerName;
            Items = items;
            DatePlaced = DateTime.Now;
        }

        public void AddItem(InventoryItem item)
        {
            Items.Add(item);
        }

        public void RemoveItem(int itemId)
        {
            Items.RemoveAll(i => i.ItemId == itemId);
        }

        public string GetOrderSummary()
        {
            return $"Order #{OrderId} for {CustomerName} | Items: {Items.Count} | Placed: {DatePlaced.ToShortDateString()}";
        }
    }
}
