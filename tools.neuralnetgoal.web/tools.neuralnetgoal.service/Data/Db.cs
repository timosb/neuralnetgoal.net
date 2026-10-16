using Microsoft.Data.SqlClient;

namespace Tools.NeuralNetGoal.Service.Data;

/// <summary>Opens connections to the existing SQL Server database. The connection string never leaves the service.</summary>
public sealed class Db(IConfiguration config)
{
    public const int CommandTimeoutSeconds = 200;

    public async Task<SqlConnection> OpenAsync(CancellationToken ct = default)
    {
        var cs = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("No database connection string (ConnectionStrings:Default). Set it with dotnet user-secrets or the host environment settings.");
        var c = new SqlConnection(cs);
        await c.OpenAsync(ct);
        return c;
    }
}
