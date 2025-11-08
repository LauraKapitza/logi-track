using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Models
{
    public class InventoryItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ItemId { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public string Location { get; set; } = string.Empty;

        // Optional: Foreign key to Order
        public int? OrderId { get; set; }

        [JsonIgnore]
        public Order? Order { get; set; }

        public InventoryItem() { }

        public InventoryItem(int itemId, string name, int quantity, string location)
        {
            ItemId = itemId;
            Name = name;
            Quantity = quantity;
            Location = location;
        }

        public string DisplayInfo()
        {
            return $"Item: {Name} | Quantity: {Quantity} | Location: {Location}";
        }
    }
}
