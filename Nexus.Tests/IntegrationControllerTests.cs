using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Nexus.Api.Controllers;
using Nexus.Api.Data;
using Nexus.Domain.Interfaces;
using Nexus.Domain.Models;
using Xunit;

namespace Nexus.Tests;

public class IntegrationControllerTests
{
    private readonly IntegrationController _controller;
    private readonly Mock<IOrderRepository> _mockRepo;
    private readonly AppDbContext _context;

    public IntegrationControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        
        _context.Tenants.Add(new Tenant 
        { 
            Id = Guid.NewGuid(), 
            ApiKey = "TEST-KEY", 
            Name = "Test Client", 
            Type = IntegrationType.FirebirdAgent 
        });
        _context.SaveChanges();

        _mockRepo = new Mock<IOrderRepository>();
        _controller = new IntegrationController(_context, _mockRepo.Object);
    }

    [Fact]
    public async Task ReceiveWebhook_ValidApiKey_ReturnsOk()
    {
        var order = new OrderDto("123", 100m, "test@test.com", DateTime.Now);
        
        var result = await _controller.ReceiveWebhook("TEST-KEY", order);

        result.Should().BeOfType<OkResult>();
        _mockRepo.Verify(r => r.EnqueueOrderAsync(It.IsAny<Guid>(), order), Times.Once);
    }

    [Fact]
    public async Task ReceiveWebhook_InvalidApiKey_ReturnsUnauthorized()
    {
        var order = new OrderDto("123", 100m, "test@test.com", DateTime.Now);
        
        var result = await _controller.ReceiveWebhook("WRONG-KEY", order);

        result.Should().BeOfType<UnauthorizedResult>();
        _mockRepo.Verify(r => r.EnqueueOrderAsync(It.IsAny<Guid>(), It.IsAny<OrderDto>()), Times.Never);
    }
}