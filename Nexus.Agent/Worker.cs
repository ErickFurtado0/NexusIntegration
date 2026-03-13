using System.Net.Http.Json;
using Nexus.Domain.Models;

namespace Nexus.Agent;

public class Worker : BackgroundService
{
    private readonly HttpClient _httpClient;
    private readonly FirebirdService _firebirdService;
    private string ApiKey => Environment.GetEnvironmentVariable("NEXUS_API_KEY") ?? "";
    public Worker()
    {
        var apiUrl = Environment.GetEnvironmentVariable("AGENT_API_BASE_URL") ?? "http://localhost:5000";
        var fbPath = Environment.GetEnvironmentVariable("FIREBIRD_DB_PATH") ?? "";

        _httpClient = new HttpClient { BaseAddress = new Uri(apiUrl) };
        _firebirdService = new FirebirdService(fbPath);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // ==========================================
                // FASE 1: PRODUTOS (FIREBIRD -> SHOPIFY)
                // ==========================================
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] A procurar produtos no Firebird...");
                var pendingProducts = _firebirdService.GetPendingProducts();
                
                if (pendingProducts.Any())
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {pendingProducts.Count} produtos encontrados! A enviar para a API...");
                    var request = new HttpRequestMessage(HttpMethod.Post, "/api/sync/products");
                    request.Headers.Add("X-Agent-ApiKey", ApiKey);
                    request.Content = JsonContent.Create(pendingProducts);

                    var response = await _httpClient.SendAsync(request, stoppingToken);
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Sucesso! Estado local dos produtos atualizado.");
                        _firebirdService.CommitSyncState(pendingProducts);
                    }
                }

                // ==========================================
                // FASE 2: PEDIDOS (SHOPIFY -> FIREBIRD)
                // ==========================================
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] A procurar novas vendas na API...");
                var requestOrders = new HttpRequestMessage(HttpMethod.Get, "/api/sync/orders");
                requestOrders.Headers.Add("X-Agent-ApiKey", ApiKey);
                
                var responseOrders = await _httpClient.SendAsync(requestOrders, stoppingToken);
                if (responseOrders.IsSuccessStatusCode)
                {
                    var pendingOrders = await responseOrders.Content.ReadFromJsonAsync<List<SyncDtos.PendingOrderDto>>(cancellationToken: stoppingToken);
                    
                    if (pendingOrders != null && pendingOrders.Any())
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🛒 {pendingOrders.Count} novos pedidos recebidos da Shopify!");
                        var processedIds = new List<Guid>();

                        foreach (var order in pendingOrders)
                        {
                            try
                            {
                                // Insere no ERP
                                _firebirdService.InsertOrder(order.ExternalId, order.JsonPayload);
                                processedIds.Add(order.QueueId);
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Pedido {order.ExternalId} gravado no Firebird.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Erro ao gravar pedido {order.ExternalId}: {ex.Message}");
                            }
                        }

                        // Avisa a API para tirar da fila os que deram sucesso
                        if (processedIds.Any())
                        {
                            var ackRequest = new HttpRequestMessage(HttpMethod.Post, "/api/sync/orders/ack");
                            ackRequest.Headers.Add("X-Agent-ApiKey", ApiKey);
                            ackRequest.Content = JsonContent.Create(processedIds);
                            await _httpClient.SendAsync(ackRequest, stoppingToken);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ERRO CRÍTICO: {ex.Message}");
            }

            await Task.Delay(10000, stoppingToken);
        }
    }
}