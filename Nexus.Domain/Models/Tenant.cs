namespace Nexus.Domain.Models;

public class Tenant
{
    public Guid Id { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public IntegrationType Type { get; set; }
    public string ConnectionConfig { get; set; } = string.Empty;
}

public enum IntegrationType
{
    FirebirdAgent,
    TotvsRemote
}