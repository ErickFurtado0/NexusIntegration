using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Nexus.Domain.Models;

namespace Nexus.Agent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly HttpClient _httpClient;
    private readonly FirebirdService _firebirdService;
    private const string ApiKey = "INTELITECH-SECRET-123"; 

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
        _firebirdService = new FirebirdService(@"C:\Intelitech\DRHS\servidor\dados\DRHS_FB25.FDB");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "/api/integration/agent/pull");
                request.Headers.Add("X-Agent-Secret", ApiKey);
                
                var response = await _httpClient.SendAsync(request, stoppingToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var orders = await response.Content.ReadFromJsonAsync<IEnumerable<OrderDto>>(cancellationToken: stoppingToken);
                    
                    if (orders != null)
                    {
                        foreach (var order in orders)
                        {
                            _firebirdService.InsertOrder(order);
                            await ConfirmProcessAsync(order.ExternalId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing with Firebird");
            }

            await Task.Delay(30000, stoppingToken);
        }
    }

    private async Task ConfirmProcessAsync(string orderId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/integration/agent/ack");
        request.Headers.Add("X-Agent-Secret", ApiKey);
        request.Content = JsonContent.Create(orderId);
        await _httpClient.SendAsync(request);
    }
}