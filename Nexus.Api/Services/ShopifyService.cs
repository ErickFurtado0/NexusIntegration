using System.Globalization;
using System.Text;
using System.Text.Json;
using Nexus.Api.Data;
using Nexus.Domain.Models;

namespace Nexus.Api.Services;

public class ShopifyService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _context;

    public ShopifyService(HttpClient httpClient, AppDbContext context)
    {
        _httpClient = httpClient;
        _context = context;
    }

    private async Task EnsureValidTokenAsync(Tenant tenant)
    {
        if (!string.IsNullOrEmpty(tenant.ShopifyAccessToken) && 
            tenant.ShopifyTokenExpiration.HasValue && 
            tenant.ShopifyTokenExpiration.Value > DateTime.UtcNow.AddMinutes(5))
        {
            return;
        }

        var requestUrl = $"https://{tenant.ShopifyStoreUrl}/admin/oauth/access_token";
        
        var requestContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", tenant.ShopifyClientId),
            new KeyValuePair<string, string>("client_secret", tenant.ShopifyClientSecret)
        });

        var response = await _httpClient.PostAsync(requestUrl, requestContent);
        var responseBody = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Falha ao gerar Token OAuth na Shopify ({response.StatusCode}): {responseBody}");
        }

        var tokenData = JsonSerializer.Deserialize<JsonElement>(responseBody);
        var accessToken = tokenData.GetProperty("access_token").GetString();
        var expiresInSeconds = tokenData.GetProperty("expires_in").GetInt32();

        tenant.ShopifyAccessToken = accessToken;
        tenant.ShopifyTokenExpiration = DateTime.UtcNow.AddSeconds(expiresInSeconds);

        _context.Tenants.Update(tenant);
        await _context.SaveChangesAsync();
    }

    public async Task ProcessProductSyncAsync(Tenant tenant, SyncDtos.ProductSyncDto productDto)
    {
        await EnsureValidTokenAsync(tenant);

        var requestUrl = $"https://{tenant.ShopifyStoreUrl}/admin/api/2026-01/products.json";
        var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        
        request.Headers.Add("X-Shopify-Access-Token", tenant.ShopifyAccessToken);

        var shopifyPayload = new
        {
            product = new
            {
                title = productDto.Name,
                status = "active",
                variants = new[]
                { 
                    new 
                    { 
                        price = productDto.Price.ToString("0.00", CultureInfo.InvariantCulture), 
                        inventory_management = "shopify",
                        inventory_quantity = productDto.StockQuantity 
                    } 
                }
            }
        };
        
        var jsonString = JsonSerializer.Serialize(shopifyPayload);
        request.Content = new StringContent(jsonString, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
            
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"Shopify recusou com erro {response.StatusCode}: {errorBody}");
        }
    }
}