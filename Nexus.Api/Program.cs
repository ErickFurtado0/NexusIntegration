using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexus.Api.Data;
using Nexus.Api.Services;
using Nexus.Domain.Interfaces;
using Nexus.Domain.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase("NexusDb"));
builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!db.Tenants.Any())
    {
        db.Tenants.Add(new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Intelitech Firebird",
            ApiKey = "INTELITECH-SECRET-123", 
            Type = IntegrationType.FirebirdAgent,
            ConnectionConfig = @"C:\Intelitech\DRHS\servidor\dados\DRHS_FB25.FDB"
        });
        db.Tenants.Add(new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Villeneuve TOTVS",
            ApiKey = "VILLENEUVE-SECRET-456",
            Type = IntegrationType.TotvsRemote,
            ConnectionConfig = "-h www30.bhan.com.br -u villeneuve -p f6r5PE9E -c 01"
        });
        db.SaveChanges();
    }
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Nexus API v1");
    c.RoutePrefix = "swagger"; 
});

app.UseAuthorization();
app.MapControllers();

app.MapGet("/", () => "Nexus API está rodando! Acesse /swagger para ver a documentação.");

var logger = app.Services.GetRequiredService<ILogger<Program>>();
app.Lifetime.ApplicationStarted.Register(() => 
{
    logger.LogInformation("🚀 API INICIADA!");
    logger.LogInformation("🔗 Swagger: /swagger");
    logger.LogInformation("🔗 Home: /");
});

app.Run();