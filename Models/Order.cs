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
        public string CustomerName { get; set; } = string.Empty;

        public DateTime DatePlaced { get; set; }

        public List<InventoryItem> Items { get; set; } = new List<InventoryItem>();

        public Order() 
        {
            DatePlaced = DateTime.Now;
        }

        public Order(int orderId, string customerName)
        {
            OrderId = orderId;
            CustomerName = customerName;
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
