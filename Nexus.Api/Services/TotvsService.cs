using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Nexus.Domain.Models;

namespace Nexus.Api.Services;

public class TotvsService
{
    private readonly HttpClient _httpClient;

    public TotvsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task SendOrderAsync(Tenant tenant, OrderDto order)
    {
        var config = ParseConnectionConfig(tenant.ConnectionConfig);

        var requestUrl = $"https://{config.Host}/api/v1/logfc006"; 

        var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        
        var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{config.User}:{config.Password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);
        request.Headers.Add("X-Totvs-Branch", config.Branch);

        var jsonContent = JsonSerializer.Serialize(new
        {
            code = order.ExternalId,
            customer = order.CustomerEmail,
            amount = order.TotalAmount,
            date = order.CreatedAt.ToString("yyyy-MM-dd")
        });

        request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private (string Host, string User, string Password, string Branch) ParseConnectionConfig(string configString)
    {
        var args = configString.Split(' ');
        string host = "", user = "", pass = "", branch = "01";

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-h" && i + 1 < args.Length) host = args[i + 1];
            if (args[i] == "-u" && i + 1 < args.Length) user = args[i + 1];
            if (args[i] == "-p" && i + 1 < args.Length) pass = args[i + 1];
            if (args[i] == "-c" && i + 1 < args.Length) branch = args[i + 1];
        }

        return (host, user, pass, branch);
    }
}