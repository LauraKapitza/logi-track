using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Models;
using Data;
using System.Text.Json.Serialization;

namespace Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly LogiTrackContext _context;

        public OrderController(LogiTrackContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrders()
        {
            var orders = await _context.Orders
                .Include(o => o.Items)
                .ToListAsync();

            return Ok(orders);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Order>> GetOrder(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                var pd = new ProblemDetails
                {
                    Type = "https://example.com/probs/not-found",
                    Title = "Order not found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = $"Order with id {id} was not found",
                    Instance = HttpContext.Request.Path
                };
                return NotFound(pd);
            }

            return Ok(order);
        }

        // Accepts an Order payload that may include:
        // - existing items referenced by ItemId (>0)
        // - new items (no ItemId or ItemId == 0)
        [HttpPost]
        public async Task<ActionResult<Order>> CreateOrder([FromBody] Order incomingOrder)
        {
            if (incomingOrder == null)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid payload",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "Request body is null or malformed",
                    Instance = HttpContext.Request.Path
                });
            }

            // Ensure collection is not null
            var incomingItems = incomingOrder.Items ?? new List<InventoryItem>();

            // Prepare final list of InventoryItem entities to attach to the Order
            var finalItems = new List<InventoryItem>();

            // Collect client-supplied IDs to resolve in a single query
            var suppliedIds = incomingItems.Where(i => i.ItemId > 0).Select(i => i.ItemId).Distinct().ToList();

            // Load existing items in one query
            var existingItemsById = new Dictionary<int, InventoryItem>();
            if (suppliedIds.Count > 0)
            {
                var existingItems = await _context.InventoryItems
                    .Where(ii => suppliedIds.Contains(ii.ItemId))
                    .ToListAsync();

                existingItemsById = existingItems.ToDictionary(ii => ii.ItemId);
            }

            // Use a transaction to ensure atomicity of the operation
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Resolve items: reference existing or add new
                foreach (var incoming in incomingItems)
                {
                    if (incoming.ItemId > 0 && existingItemsById.TryGetValue(incoming.ItemId, out var existing))
                    {
                        // Use the tracked existing item
                        finalItems.Add(existing);
                    }
                    else
                    {
                        incoming.ItemId = 0; // force DB-generated id

                        // Avoid carrying circular reference into tracked graph now
                        incoming.Order = null;

                        // Add new item to context (will be inserted)
                        await _context.InventoryItems.AddAsync(incoming);
                        finalItems.Add(incoming);
                    }
                }

                // Prepare the Order entity
                incomingOrder.OrderId = 0; // force DB-generated id
                incomingOrder.Items = finalItems;
                incomingOrder.DatePlaced = incomingOrder.DatePlaced == default ? DateTime.UtcNow : incomingOrder.DatePlaced;

                // Add order and save
                await _context.Orders.AddAsync(incomingOrder);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                // Return created resource (GetOrder will return order with items)
                return CreatedAtAction(nameof(GetOrder), new { id = incomingOrder.OrderId }, incomingOrder);
            }
            catch (DbUpdateException dbEx)
            {
                await transaction.RollbackAsync();

                // Log the exception in real app (omitted here)
                var pd = new ProblemDetails
                {
                    Title = "Database update failed",
                    Status = StatusCodes.Status500InternalServerError,
                    Detail = dbEx.Message,
                    Instance = HttpContext.Request.Path
                };
                return StatusCode(StatusCodes.Status500InternalServerError, pd);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                var pd = new ProblemDetails
                {
                    Title = "Unexpected error",
                    Status = StatusCodes.Status500InternalServerError,
                    Detail = ex.Message,
                    Instance = HttpContext.Request.Path
                };
                return StatusCode(StatusCodes.Status500InternalServerError, pd);
            }
        }

        // DELETE: /api/orders/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                // Idempotent behavior — treat already-absent as success
                return NoContent();
            }

            // Remove related items if they are owned by the order
            _context.InventoryItems.RemoveRange(order.Items);
            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
