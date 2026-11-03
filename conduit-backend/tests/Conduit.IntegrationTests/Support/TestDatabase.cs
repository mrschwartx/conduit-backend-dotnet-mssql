using Microsoft.Data.SqlClient;
using Conduit.SharedKernel.Infrastructure.Persistence;

namespace Conduit.IntegrationTests.Support;

public sealed class TestDatabase : IAsyncDisposable
{
    private const string DefaultUrl = "sqlserver://sa:Dev_Pass123@localhost:1433/master?trustServerCertificate=true";

    private readonly string adminConnectionString;

    private TestDatabase(string name, string adminConnectionString, string connectionString)
    {
        Name = name;
        this.adminConnectionString = adminConnectionString;
        ConnectionString = connectionString;
    }

    public string Name { get; }

    public string ConnectionString { get; }

    public static async Task<TestDatabase> CreateAsync()
    {
        var url = Environment.GetEnvironmentVariable("TEST_DATABASE_URL");
        var admin = new SqlConnectionStringBuilder(
            SqlServerConnectionString.Build(string.IsNullOrWhiteSpace(url) ? DefaultUrl : url, 5, 20000))
        {
            InitialCatalog = "master"
        };
        var adminConnectionString = admin.ConnectionString;
        var name = "conduit_test_" + Guid.NewGuid().ToString("N");

        await using (var connection = new SqlConnection(adminConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new SqlCommand($"CREATE DATABASE [{name}]", connection);
            await command.ExecuteNonQueryAsync();
        }

        var builder = new SqlConnectionStringBuilder(adminConnectionString) { InitialCatalog = name };
        return new TestDatabase(name, adminConnectionString, builder.ConnectionString);
    }

    public async Task<bool> TableExistsAsync(SqlServerDataSource dataSource, string table)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand(
            "SELECT CAST(CASE WHEN OBJECT_ID(@table, N'U') IS NOT NULL THEN 1 ELSE 0 END AS bit)");
        command.AddParameter("table", System.Data.SqlDbType.NVarChar, table);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    public async ValueTask DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            $"IF DB_ID(N'{Name}') IS NOT NULL BEGIN "
            + $"ALTER DATABASE [{Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; "
            + $"DROP DATABASE [{Name}]; END",
            connection);
        await command.ExecuteNonQueryAsync();
    }
}
