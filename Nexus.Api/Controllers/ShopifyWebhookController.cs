using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Api.Data;
using Nexus.Domain.Models;

namespace Nexus.Api.Controllers;

[ApiController]
[Route("api/webhooks/shopify")]
public class ShopifyWebhookController : ControllerBase
{
    private readonly AppDbContext _context;

    public ShopifyWebhookController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> ReceiveWebhook()
    {
        if (!Request.Headers.TryGetValue("X-Shopify-Shop-Domain", out var shopDomain) ||
            !Request.Headers.TryGetValue("X-Shopify-Topic", out var topic))
        {
            return BadRequest();
        }

        var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.ShopifyStoreUrl == shopDomain.ToString());
        if (tenant == null)
        {
            return Unauthorized();
        }

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        var topicString = topic.ToString();

        try
        {
            await RouteWebhookTopicAsync(topicString, tenant, body);
            return Ok();
        }
        catch
        {
            return StatusCode(500);
        }
    }

    private async Task RouteWebhookTopicAsync(string topic, Tenant tenant, string payload)
    {
        switch (topic)
        {
            case "orders/create":
                await HandleOrderCreatedAsync(tenant, payload);
                break;
            case "orders/updated":
                await HandleOrderUpdatedAsync(tenant, payload);
                break;
            case "orders/cancelled":
                await HandleOrderCancelledAsync(tenant, payload);
                break;
            case "products/create":
                await HandleProductCreatedAsync(tenant, payload);
                break;
            case "products/update":
                await HandleProductUpdatedAsync(tenant, payload);
                break;
            case "products/delete":
                await HandleProductDeletedAsync(tenant, payload);
                break;
            default:
                break;
        }
    }

    private async Task HandleOrderCreatedAsync(Tenant tenant, string payload)
    {
        var json = JsonSerializer.Deserialize<JsonElement>(payload);
        var orderId = json.GetProperty("id").ToString();
        await EnqueuePayloadAsync(tenant.Id, orderId, "ORDER_CREATE", payload);
    }

    private async Task HandleOrderUpdatedAsync(Tenant tenant, string payload)
    {
        var json = JsonSerializer.Deserialize<JsonElement>(payload);
        var orderId = json.GetProperty("id").ToString();
        await EnqueuePayloadAsync(tenant.Id, orderId, "ORDER_UPDATE", payload);
    }

    private async Task HandleOrderCancelledAsync(Tenant tenant, string payload)
    {
        var json = JsonSerializer.Deserialize<JsonElement>(payload);
        var orderId = json.GetProperty("id").ToString();
        await EnqueuePayloadAsync(tenant.Id, orderId, "ORDER_CANCEL", payload);
    }

    private async Task HandleProductCreatedAsync(Tenant tenant, string payload)
    {
        var json = JsonSerializer.Deserialize<JsonElement>(payload);
        var productId = json.GetProperty("id").ToString();
        await EnqueuePayloadAsync(tenant.Id, productId, "PRODUCT_CREATE", payload);
    }

    private async Task HandleProductUpdatedAsync(Tenant tenant, string payload)
    {
        var json = JsonSerializer.Deserialize<JsonElement>(payload);
        var productId = json.GetProperty("id").ToString();
        await EnqueuePayloadAsync(tenant.Id, productId, "PRODUCT_UPDATE", payload);
    }

    private async Task HandleProductDeletedAsync(Tenant tenant, string payload)
    {
        var json = JsonSerializer.Deserialize<JsonElement>(payload);
        var productId = json.GetProperty("id").ToString();
        await EnqueuePayloadAsync(tenant.Id, productId, "PRODUCT_DELETE", payload);
    }

    private async Task EnqueuePayloadAsync(Guid tenantId, string externalId, string type, string payload)
    {
        var queueItem = new OrderQueue
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderExternalId = $"{type}_{externalId}",
            JsonPayload = payload,
            IsProcessed = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.OrderQueues.Add(queueItem);
        await _context.SaveChangesAsync();
    }
}