using Nexus.Domain.Models;

namespace Nexus.Domain.Interfaces;

public interface IOrderRepository
{
    Task EnqueueOrderAsync(Guid tenantId, OrderDto order);
    Task<IEnumerable<OrderDto>> GetPendingOrdersAsync(Guid tenantId);
    Task MarkAsProcessedAsync(Guid tenantId, string externalId);
}