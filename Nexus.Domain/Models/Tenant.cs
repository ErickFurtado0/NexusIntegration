namespace Nexus.Domain.Models;

public class Tenant
{
    public Guid Id { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ConnectionConfig { get; set; } = string.Empty;
    
    public string ShopifyStoreUrl { get; set; } = string.Empty;
    
    public string ShopifyClientId { get; set; } = string.Empty;
    public string ShopifyClientSecret { get; set; } = string.Empty;
    
    public string ShopifyAccessToken { get; set; } = string.Empty;
    public DateTime? ShopifyTokenExpiration { get; set; }
}