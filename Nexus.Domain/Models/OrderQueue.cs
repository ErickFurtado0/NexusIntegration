namespace Nexus.Domain.Models;

public class OrderQueue
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string OrderExternalId { get; set; } = string.Empty;
    public string JsonPayload { get; set; } = string.Empty;
    public bool IsProcessed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}