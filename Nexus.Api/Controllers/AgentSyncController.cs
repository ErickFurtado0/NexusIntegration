using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Api.Data;
using Nexus.Api.Services;
using Nexus.Domain.Models;

namespace Nexus.Api.Controllers;

[ApiController]
[Route("api/sync")]
public class AgentSyncController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ShopifyService _shopifyService;
    private readonly string _logDirectory;

    public AgentSyncController(AppDbContext context, ShopifyService shopifyService)
    {
        _context = context;
        _shopifyService = shopifyService;
        _logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
    }
    
    #region FB to Shopify
    [HttpPost("products")]
    public async Task<IActionResult> SyncProducts([FromHeader(Name = "X-Agent-ApiKey")] string apiKey, [FromBody] List<SyncDtos.ProductSyncDto> products)
    {
        await WriteLogAsync("INICIO", $"Recebida requisição de sync com {products.Count} produtos.");

        var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.ApiKey == apiKey);
        if (tenant == null)
        {
            await WriteLogAsync("ERRO", "Tentativa de sincronização com API Key inválida.");
            return Unauthorized();
        }

        int successCount = 0;
        int errorCount = 0;

        foreach (var product in products)
        {
            try
            {
                await _shopifyService.ProcessProductSyncAsync(tenant, product);
                successCount++;
                await WriteLogAsync("SUCESSO", $"Produto {product.ExternalId} sincronizado na Shopify.");
            }
            catch (Exception ex)
            {
                errorCount++;
                await WriteLogAsync("ERRO", $"Falha ao sincronizar produto {product.ExternalId}: {ex.Message}");
            }
        }

        await WriteLogAsync("FIM", $"Sincronização concluída. Sucesso: {successCount} | Erros: {errorCount}");

        return Ok(new { success = successCount, errors = errorCount });
    }
    #endregion
    
    #region Shopify to FB
    [HttpGet("orders")]
    public async Task<IActionResult> GetPendingOrders([FromHeader(Name = "X-Agent-ApiKey")] string apiKey)
    {
        var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.ApiKey == apiKey);
        if (tenant == null) return Unauthorized();

        var pendingOrders = await _context.OrderQueues
            .Where(q => q.TenantId == tenant.Id && !q.IsProcessed)
            .OrderBy(q => q.CreatedAt)
            .Take(50) 
            .Select(q => new SyncDtos.PendingOrderDto(q.Id, q.OrderExternalId, q.JsonPayload))
            .ToListAsync();

        return Ok(pendingOrders);
    }

    [HttpPost("orders/ack")]
    public async Task<IActionResult> AcknowledgeOrders([FromHeader(Name = "X-Agent-ApiKey")] string apiKey, [FromBody] List<Guid> processedIds)
    {
        var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.ApiKey == apiKey);
        if (tenant == null) return Unauthorized();

        var ordersToUpdate = await _context.OrderQueues
            .Where(q => q.TenantId == tenant.Id && processedIds.Contains(q.Id))
            .ToListAsync();

        foreach (var order in ordersToUpdate)
        {
            order.IsProcessed = true;
        }

        await _context.SaveChangesAsync();
        return Ok();
    }
    #endregion

    #region Privates Methods
    private async Task WriteLogAsync(string level, string message)
    {
        try
        {
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            var filePath = Path.Combine(_logDirectory, $"SyncLog_{DateTime.Now:yyyy-MM-dd}.txt");
            var logEntry = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}{Environment.NewLine}";

            await System.IO.File.AppendAllTextAsync(filePath, logEntry);
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }
    #endregion
}