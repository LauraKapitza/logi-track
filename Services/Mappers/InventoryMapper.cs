using Dtos;
using Models;
using System.Collections.Generic;
using System.Linq;

namespace Services.Mappers
{
    public class InventoryMapper : IInventoryMapper
    {
        public InventoryItemDto ToDto(InventoryItem entity) =>
            new InventoryItemDto
            {
                ItemId = entity.ItemId,
                Name = entity.Name,
                Quantity = entity.Quantity,
                Location = entity.Location
            };

        public InventoryItem ToEntity(InventoryItemDto dto) =>
            new InventoryItem
            {
                // ItemId left to EF when creating new entity
                Name = dto.Name,
                Quantity = dto.Quantity,
                Location = dto.Location
            };

        public IEnumerable<InventoryItemDto> ToDtoList(IEnumerable<InventoryItem> entities) =>
            entities.Select(ToDto);
    }
}
