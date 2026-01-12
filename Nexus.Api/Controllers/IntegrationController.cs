using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Api.Data;
using Nexus.Domain.Interfaces;
using Nexus.Domain.Models;

namespace Nexus.Api.Controllers;

[ApiController]
[Route("api/integration")]
public class IntegrationController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IOrderRepository _repository;

    public IntegrationController(AppDbContext context, IOrderRepository repository)
    {
        _context = context;
        _repository = repository;
    }

    [HttpPost("webhook/shopify")]
    public async Task<IActionResult> ReceiveWebhook([FromHeader(Name = "X-Tenant-ApiKey")] string apiKey, [FromBody] OrderDto order)
    {
        var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.ApiKey == apiKey);
        if (tenant == null) return Unauthorized();

        await _repository.EnqueueOrderAsync(tenant.Id, order);
        return Ok();
    }

    [HttpGet("agent/pull")]
    public async Task<IActionResult> PullPendingOrders([FromHeader(Name = "X-Agent-Secret")] string apiKey)
    {
        var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.ApiKey == apiKey);
        if (tenant == null) return Unauthorized();

        var orders = await _repository.GetPendingOrdersAsync(tenant.Id);
        return Ok(orders);
    }
    
    [HttpPost("agent/ack")]
    public async Task<IActionResult> AcknowledgeOrder([FromHeader(Name = "X-Agent-Secret")] string apiKey, [FromBody] string orderId)
    {
        var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.ApiKey == apiKey);
        if (tenant == null) return Unauthorized();

        await _repository.MarkAsProcessedAsync(tenant.Id, orderId);
        return Ok();
    }
}