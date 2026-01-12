namespace Nexus.Domain.Models;

public record OrderDto(string ExternalId, decimal TotalAmount, string CustomerEmail, DateTime CreatedAt);