using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FirebirdSql.Data.FirebirdClient;
using Nexus.Domain.Models;

namespace Nexus.Agent;

public class FirebirdService
{
    private readonly string _connectionString;
    private readonly string _stateFilePath;
    private Dictionary<string, string> _localState;

    public FirebirdService(string fdbPath)
    {
        var builder = new FbConnectionStringBuilder 
        { 
            Database = fdbPath, 
            UserID = "SYSDBA", 
            Password = "masterkey", 
            DataSource = "localhost", 
            Port = 3050, 
            Dialect = 3 
        };
        _connectionString = builder.ToString();
        _stateFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sync_state.json");
        _localState = LoadState();
    }

    public List<SyncDtos.ProductSyncDto> GetPendingProducts()
    {
        var pendingProducts = new List<SyncDtos.ProductSyncDto>();
        using var connection = new FbConnection(_connectionString);
        connection.Open();

        var sql = "SELECT ID_PRODUTO, DESCRICAO, PRECO, ESTOQUE FROM PRODUTOS";
        using var cmd = new FbCommand(sql, connection);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var id = reader["ID_PRODUTO"].ToString() ?? string.Empty;
            var name = reader["DESCRICAO"].ToString() ?? string.Empty;
            var price = Convert.ToDecimal(reader["PRECO"]);
            var stock = Convert.ToInt32(reader["ESTOQUE"]);

            var currentHash = GenerateHash($"{id}|{name}|{price}|{stock}");

            if (!_localState.TryGetValue(id, out var savedHash) || savedHash != currentHash)
            {
                var action = savedHash == null ? SyncDtos.SyncAction.Create : SyncDtos.SyncAction.Update;
                
                pendingProducts.Add(new SyncDtos.ProductSyncDto(id, name, price, stock, action));
            }
        }

        return pendingProducts;
    }

    public void CommitSyncState(IEnumerable<SyncDtos.ProductSyncDto> syncedProducts)
    {
        foreach (var product in syncedProducts)
        {
            var hash = GenerateHash($"{product.ExternalId}|{product.Name}|{product.Price}|{product.StockQuantity}");
            _localState[product.ExternalId] = hash;
        }

        SaveState();
    }
    
    public void InsertOrder(string externalId, string jsonPayload)
    {
        using var doc = JsonDocument.Parse(jsonPayload);
        var root = doc.RootElement;
        
        var orderId = root.GetProperty("id").ToString();
        var totalPrice = root.GetProperty("current_total_price").GetString();
        var customerEmail = root.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : "sem-email@loja.com";

        using var connection = new FbConnection(_connectionString);
        connection.Open();

        var sql = @"
            INSERT INTO PEDIDOS (ID_PEDIDO_EXTERNO, VALOR_TOTAL, EMAIL_CLIENTE) 
            VALUES (@id, @valor, @email)";
        
        using var cmd = new FbCommand(sql, connection);
        cmd.Parameters.AddWithValue("@id", externalId);
        cmd.Parameters.AddWithValue("@valor", Convert.ToDecimal(totalPrice, System.Globalization.CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@email", customerEmail);
        
        cmd.ExecuteNonQuery();
    }

    private Dictionary<string, string> LoadState()
    {
        if (!File.Exists(_stateFilePath)) return new Dictionary<string, string>();
        var json = File.ReadAllText(_stateFilePath);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
    }

    private void SaveState()
    {
        var json = JsonSerializer.Serialize(_localState);
        File.WriteAllText(_stateFilePath, json);
    }

    private string GenerateHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }
}