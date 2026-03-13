using DotNetEnv;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexus.Api.Data;
using Nexus.Api.Services;
using Nexus.Domain.Models;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase("NexusDb"));
builder.Services.AddHttpClient<ShopifyService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!db.Tenants.Any())
    {
        db.Tenants.Add(new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Cliente Firebird Principal",
            ApiKey = Environment.GetEnvironmentVariable("NEXUS_API_KEY") ?? "ERRO_SEM_CHAVE", 
            ConnectionConfig = Environment.GetEnvironmentVariable("FIREBIRD_DB_PATH") ?? "",
            ShopifyStoreUrl = Environment.GetEnvironmentVariable("SHOPIFY_STORE_URL") ?? "",
            ShopifyClientId = Environment.GetEnvironmentVariable("SHOPIFY_CLIENT_ID") ?? "",
            ShopifyClientSecret = Environment.GetEnvironmentVariable("SHOPIFY_CLIENT_SECRET") ?? ""
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
app.MapGet("/", () => "Nexus API Operational.");

app.Run("http://localhost:5000");

var logger = app.Services.GetRequiredService<ILogger<Program>>();
app.Lifetime.ApplicationStarted.Register(() => 
{
    logger.LogInformation("🚀 API INICIADA!");
    logger.LogInformation("🔗 Swagger: /swagger");
    logger.LogInformation("🔗 Home: /");
});