using Microsoft.EntityFrameworkCore;
using Models;

namespace Data
{
    public class LogiTrackContext : DbContext
    {
        public DbSet<InventoryItem> InventoryItems { get; set; }
        public DbSet<Order> Orders { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite("Data Source=logitrack.db");
    }
}
