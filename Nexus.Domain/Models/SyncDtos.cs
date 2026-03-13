namespace Nexus.Domain.Models;

public class SyncDtos
{
    public enum SyncAction 
    {
        Create = 1,
        Update = 2,
        Delete = 3
    }

    public record ProductSyncDto(string ExternalId, string Name, decimal Price, int StockQuantity, SyncAction Action);
    
    public record PendingOrderDto(
        Guid QueueId,
        string ExternalId,
        string JsonPayload
    );
    
}