using Dtos;
using Models;
using System.Collections.Generic;

namespace Services.Mappers
{
    public interface IInventoryMapper
    {
        InventoryItemDto ToDto(InventoryItem entity);
        InventoryItem ToEntity(InventoryItemDto dto);
        IEnumerable<InventoryItemDto> ToDtoList(IEnumerable<InventoryItem> entities);
    }
}
