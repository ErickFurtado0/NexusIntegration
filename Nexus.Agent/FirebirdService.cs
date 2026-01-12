using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Nexus.Domain.Models;

namespace Nexus.Agent;

public class FirebirdService
{
    private readonly string _connectionString;

    public FirebirdService(string fdbPath)
    {
        var builder = new FbConnectionStringBuilder
        {
            Database = fdbPath,
            DataSource = "localhost",
            UserID = "SYSDBA",
            Password = "masterkey",
            ServerType = FbServerType.Default,
            Port = 3050,
            Dialect = 3,
            Charset = "NONE"
        };
        _connectionString = builder.ToString();
    }

    public void InsertOrder(OrderDto order)
    {
        using var connection = new FbConnection(_connectionString);
        connection.Open();
        
        using var transaction = connection.BeginTransaction();
        try
        {
            using var cmd = new FbCommand("INSERT INTO PEDIDOS (ID_EXTERNO, DATA, VALOR, CLIENTE) VALUES (@id, @dt, @val, @cli)", connection, transaction);
            cmd.Parameters.Add("@id", FbDbType.VarChar).Value = order.ExternalId;
            cmd.Parameters.Add("@dt", FbDbType.TimeStamp).Value = order.CreatedAt;
            cmd.Parameters.Add("@val", FbDbType.Decimal).Value = order.TotalAmount;
            cmd.Parameters.Add("@cli", FbDbType.VarChar).Value = order.CustomerEmail;

            cmd.ExecuteNonQuery();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}