using Npgsql;
using Conduit.SharedKernel.Infrastructure.Persistence;

namespace Conduit.IntegrationTests.Support;

public sealed class TestDatabase : IAsyncDisposable
{
    private const string DefaultUrl = "postgres://dev_user:dev_pass@localhost:5432/conduit_dev";

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
        var adminConnectionString = PostgresConnectionString.Build(string.IsNullOrWhiteSpace(url) ? DefaultUrl : url, 5, 20000);
        var name = "conduit_test_" + Guid.NewGuid().ToString("N");

        await using (var connection = new NpgsqlConnection(adminConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = name };
        return new TestDatabase(name, adminConnectionString, builder.ConnectionString);
    }

    public async Task<bool> TableExistsAsync(NpgsqlDataSource dataSource, string table)
    {
        await using var command = dataSource.CreateCommand($"SELECT to_regclass('public.{table}') IS NOT NULL");
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}
