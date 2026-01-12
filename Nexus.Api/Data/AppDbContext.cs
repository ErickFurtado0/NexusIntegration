using Microsoft.EntityFrameworkCore;
using Nexus.Domain.Models;

namespace Nexus.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Tenant> Tenants { get; set; }
}