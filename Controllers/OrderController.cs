using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Models;
using Data;
using Dtos;
using Services.Mappers;
using Swashbuckle.AspNetCore.Annotations;

namespace Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly LogiTrackContext _context;
        private readonly IMemoryCache _cache;
        private readonly IOrderMapper _mapper;

        // Cache configuration
        private const string OrdersListVersionKey = "Orders:List:Version";
        private const string OrdersListCacheKeyFormat = "Orders:List:v={0}";
        private const string OrderByIdCacheKeyFormat = "Orders:Id:{0}";
        private static readonly TimeSpan OrdersListTtl = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan OrderByIdTtl = TimeSpan.FromSeconds(60);

        public OrderController(LogiTrackContext context, IMemoryCache cache, IOrderMapper mapper)
        {
            _context = context;
            _cache = cache;
            _mapper = mapper;
        }

        // GET: /api/Order
        [HttpGet]
        [SwaggerOperation(Summary = "List orders", Description = "Returns a list of orders (cached).")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(IEnumerable<OrderDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<OrderDto>>> GetOrders()
        {
            // Version token strategy for list invalidation
            var version = _cache.GetOrCreate<string>(OrdersListVersionKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);
                return Guid.NewGuid().ToString();
            });

            var cacheKey = string.Format(OrdersListCacheKeyFormat, version);

            if (_cache.TryGetValue<List<OrderDto>>(cacheKey, out var cached) && cached is not null)
            {
                return Ok(cached);
            }

            // Project to DTO directly in the query for performance (no change-tracking)
            var orders = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .OrderByDescending(o => o.DatePlaced)
                .Select(o => new OrderDto
                {
                    OrderId = o.OrderId,
                    CustomerName = o.CustomerName,
                    DatePlaced = o.DatePlaced,
                    Items = o.Items.Select(ii => new InventoryItemDto
                    {
                        ItemId = ii.ItemId,
                        Name = ii.Name,
                        Quantity = ii.Quantity,
                        Location = ii.Location
                    }).ToList()
                })
                .ToListAsync();

            _cache.Set(cacheKey, orders, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = OrdersListTtl });

            return Ok(orders);
        }

        // GET: /api/Order/{id}
        [HttpGet("{id}")]
        [SwaggerOperation(Summary = "Get order by id", Description = "Returns a single order by id (cached per id).")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<OrderDto>> GetOrder(int id)
        {
            var cacheKey = string.Format(OrderByIdCacheKeyFormat, id);

            if (_cache.TryGetValue<OrderDto>(cacheKey, out var cached) && cached is not null)
            {
                return Ok(cached);
            }

            var order = await _context.Orders
                .AsNoTracking()
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

            var dto = _mapper.ToDto(order);

            _cache.Set(cacheKey, dto, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = OrderByIdTtl });

            return Ok(dto);
        }

        // POST: /api/Order
        [HttpPost]
        [Authorize(Roles = "Manager")]
        [SwaggerOperation(Summary = "Create order", Description = "Creates an order. Accepts existing or new inventory items. Invalidates order caches.")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] Order incomingOrder)
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

            // Validate required fields
            if (string.IsNullOrWhiteSpace(incomingOrder.CustomerName))
            {
                ModelState.AddModelError(nameof(incomingOrder.CustomerName), "CustomerName is required.");
            }

            if (incomingOrder.Items == null || incomingOrder.Items.Count == 0)
            {
                ModelState.AddModelError(nameof(incomingOrder.Items), "At least one item is required for an order.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Resolve incoming items into a final collection of InventoryItem entities
            var incomingItems = incomingOrder.Items ?? new List<InventoryItem>();
            var finalItems = new List<InventoryItem>();

            var suppliedIds = incomingItems.Where(i => i.ItemId > 0).Select(i => i.ItemId).Distinct().ToList();

            var existingItemsById = new Dictionary<int, InventoryItem>();
            if (suppliedIds.Count > 0)
            {
                var existingItems = await _context.InventoryItems
                    .Where(ii => suppliedIds.Contains(ii.ItemId))
                    .ToListAsync();

                existingItemsById = existingItems.ToDictionary(ii => ii.ItemId);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                foreach (var incoming in incomingItems)
                {
                    if (incoming.ItemId > 0 && existingItemsById.TryGetValue(incoming.ItemId, out var existing))
                    {
                        finalItems.Add(existing);
                    }
                    else
                    {
                        incoming.ItemId = 0;
                        incoming.Order = null;
                        await _context.InventoryItems.AddAsync(incoming);
                        finalItems.Add(incoming);
                    }
                }

                var orderEntity = _mapper.CreateEntityForPersistence(incomingOrder, finalItems);

                await _context.Orders.AddAsync(orderEntity);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                // Invalidate list caches and set per-order cache for immediate reads
                _cache.Set(OrdersListVersionKey, Guid.NewGuid().ToString(), TimeSpan.FromDays(1));

                var createdDto = _mapper.ToDto(orderEntity);
                var createdCacheKey = string.Format(OrderByIdCacheKeyFormat, createdDto.OrderId);
                _cache.Set(createdCacheKey, createdDto, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = OrderByIdTtl });

                return CreatedAtAction(nameof(GetOrder), new { id = createdDto.OrderId }, createdDto);
            }
            catch (DbUpdateException dbEx)
            {
                await transaction.RollbackAsync();

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

        // DELETE: /api/Order/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Manager")]
        [SwaggerOperation(Summary = "Delete order", Description = "Deletes an order and its owned items; invalidates caches.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                // idempotent behavior
                return NoContent();
            }

            _context.InventoryItems.RemoveRange(order.Items);
            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();

            // Invalidate caches: bump list version and remove per-order cache
            _cache.Set(OrdersListVersionKey, Guid.NewGuid().ToString(), TimeSpan.FromDays(1));
            var perOrderKey = string.Format(OrderByIdCacheKeyFormat, id);
            _cache.Remove(perOrderKey);

            return NoContent();
        }
    }
}
