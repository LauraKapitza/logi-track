using System.Collections.Generic;
using Dtos;
using Models;

namespace Services.Mappers
{
    public interface IOrderMapper
    {
        OrderDto ToDto(Order entity);
        List<OrderDto> ToDtoList(IEnumerable<Order> entities);

        Order CreateEntityForPersistence(Order incomingOrder, IEnumerable<InventoryItem> finalItems);
    }
}
