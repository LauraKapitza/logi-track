using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Http;
using Services.Mappers;
using Models;
using Data;
using Dtos;

namespace Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InventoryController : ControllerBase
    {
        private readonly LogiTrackContext _context;
        private readonly IMemoryCache _cache;
        private readonly IInventoryMapper _mapper;

        // Cache keys / TTL
        private const string InventoryListVersionKey = "Inventory:List:Version";
        private const string InventoryListCacheKeyFormat = "Inventory:List:v={0}";
        private static readonly TimeSpan InventoryListTtl = TimeSpan.FromSeconds(60);

        public InventoryController(LogiTrackContext context, IMemoryCache cache, IInventoryMapper mapper)
        {
            _context = context;
            _cache = cache;
            _mapper = mapper;
        }
    

        // GET: /api/inventory
        [HttpGet]
        public async Task<ActionResult<IEnumerable<InventoryItemDto>>> GetInventory()
        {
            // Version token approach for invalidation
            var version = _cache.GetOrCreate<string>(InventoryListVersionKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);
                return Guid.NewGuid().ToString();
            });

            var cacheKey = string.Format(InventoryListCacheKeyFormat, version);

            if (_cache.TryGetValue<List<InventoryItemDto>>(cacheKey, out var cachedDtos) && cachedDtos is not null)
            {
                return Ok(cachedDtos);
            }

            // Project to DTO at the database level to avoid loading full tracked entities
            var items = await _context.InventoryItems
                                 .AsNoTracking()
                                 .OrderBy(i => i.ItemId)
                                 .Select(i => new InventoryItemDto
                                 {
                                     ItemId = i.ItemId,
                                     Name = i.Name,
                                     Quantity = i.Quantity,
                                     Location = i.Location
                                 })
                                 .ToListAsync();

            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = InventoryListTtl
            };

            _cache.Set(cacheKey, items, cacheEntryOptions);

            return Ok(items);
        }

        // POST: /api/Inventory
        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<InventoryItemDto>> AddInventoryItem([FromBody] InventoryItemDto createDto)
        {
            if (createDto == null || !ModelState.IsValid)
                return BadRequest(ModelState);

            var entity = _mapper.ToEntity(createDto);

            await _context.InventoryItems.AddAsync(entity);
            await _context.SaveChangesAsync();

            _cache.Set(InventoryListVersionKey, Guid.NewGuid().ToString(), TimeSpan.FromDays(1));

            var resultDto = _mapper.ToDto(entity);

            return CreatedAtAction(nameof(GetInventory), new { id = resultDto.ItemId }, resultDto);
        }

        // DELETE: /api/inventory/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> DeleteInventoryItem(int id)
        {
            var item = await _context.InventoryItems.FindAsync(id);
            if (item == null)
                return NotFound(new ProblemDetails
                {
                    Title = "Item not found",
                    Detail = $"InventoryItem {id} does not exist",
                    Status = StatusCodes.Status404NotFound,
                    Instance = HttpContext.Request.Path
                });

            _context.InventoryItems.Remove(item);
            await _context.SaveChangesAsync();

            // Bump version token to invalidate cached lists
            _cache.Set(InventoryListVersionKey, Guid.NewGuid().ToString(), TimeSpan.FromDays(1));

            // Remove any per-item cache if you introduce one later: _cache.Remove($"Inventory:Id:{id}")

            return NoContent();
        }
    }
}
