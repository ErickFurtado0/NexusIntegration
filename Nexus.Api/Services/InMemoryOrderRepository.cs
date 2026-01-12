using System.Collections.Concurrent;
using Nexus.Domain.Interfaces;
using Nexus.Domain.Models;

namespace Nexus.Api.Services;

public class InMemoryOrderRepository : IOrderRepository
{
    private static readonly ConcurrentDictionary<Guid, ConcurrentQueue<OrderDto>> _queues = new();

    public Task EnqueueOrderAsync(Guid tenantId, OrderDto order)
    {
        _queues.TryAdd(tenantId, new ConcurrentQueue<OrderDto>());
        _queues[tenantId].Enqueue(order);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<OrderDto>> GetPendingOrdersAsync(Guid tenantId)
    {
        if (_queues.TryGetValue(tenantId, out var queue))
        {
            return Task.FromResult(queue.AsEnumerable());
        }
        return Task.FromResult(Enumerable.Empty<OrderDto>());
    }

    public Task MarkAsProcessedAsync(Guid tenantId, string externalId)
    {
        if (_queues.TryGetValue(tenantId, out var queue))
        {
            queue.TryDequeue(out _);
        }
        return Task.CompletedTask;
    }
}