using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Data;

namespace Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly LogiTrackContext _context;

        public InventoryController(LogiTrackContext context)
        {
            _context = context;
        }

        // GET: /api/inventory
        [HttpGet]
        public async Task<ActionResult<IEnumerable<InventoryItem>>> GetInventory()
        {
            var items = await _context.InventoryItems.ToListAsync();
            return Ok(items);
        }

        // POST: /api/inventory
        [HttpPost]
        public async Task<ActionResult<InventoryItem>> AddInventoryItem([FromBody] InventoryItem item)
        {
            // Ensure server-side id generation if needed
            item.ItemId = 0;

            await _context.InventoryItems.AddAsync(item);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetInventory), new { id = item.ItemId }, item);
        }

        // DELETE: /api/inventory/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteInventoryItem(int id)
        {
            var item = await _context.InventoryItems.FindAsync(id);
            if (item == null)
                return NotFound(new ProblemDetails { Title = "Item not found", Detail = $"InventoryItem {id} does not exist", Status = StatusCodes.Status404NotFound, Instance = HttpContext.Request.Path });

            _context.InventoryItems.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
