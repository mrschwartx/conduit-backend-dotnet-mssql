using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Conduit.SharedKernel.Infrastructure.Persistence;

public sealed class SqlMigrator(
    SqlServerDataSource dataSource,
    IEnumerable<IMigrationSource> sources,
    ILogger<SqlMigrator> logger)
{
    private const string LockResource = "conduit_schema_migrations";

    private const string CreateMigrationsTable =
        "IF OBJECT_ID(N'schema_migrations', N'U') IS NULL "
        + "CREATE TABLE schema_migrations ("
        + "name NVARCHAR(450) NOT NULL PRIMARY KEY, "
        + "applied_at DATETIMEOFFSET(3) NOT NULL DEFAULT SYSDATETIMEOFFSET())";

    public async Task<IReadOnlyList<string>> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var scripts = CollectScripts();
        var executed = new List<string>();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await AcquireLockAsync(connection, cancellationToken);
        try
        {
            await RunAsync(connection, CreateMigrationsTable, cancellationToken);
            var applied = await LoadAppliedAsync(connection, cancellationToken);

            foreach (var script in scripts)
            {
                if (applied.Contains(script.Name))
                {
                    continue;
                }

                await ApplyAsync(connection, script, cancellationToken);
                executed.Add(script.Name);
                logger.LogInformation("Applied migration {Migration}", script.Name);
            }
        }
        finally
        {
            await ReleaseLockAsync(connection);
        }

        return executed;
    }

    private List<MigrationScript> CollectScripts()
    {
        var scripts = sources
            .SelectMany(source => source.Scripts())
            .OrderBy(script => script.Name, StringComparer.Ordinal)
            .ToList();

        var duplicate = scripts
            .GroupBy(script => script.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Duplicate migration {duplicate.Key}");
        }

        return scripts;
    }

    private static async Task RunAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task AcquireLockAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand("sp_getapplock");
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = 0;
        command.AddParameter("Resource", SqlDbType.NVarChar, LockResource);
        command.AddParameter("LockMode", SqlDbType.VarChar, "Exclusive");
        command.AddParameter("LockOwner", SqlDbType.VarChar, "Session");
        command.AddParameter("LockTimeout", SqlDbType.Int, -1);
        var result = command.Parameters.Add("@Result", SqlDbType.Int);
        result.Direction = ParameterDirection.ReturnValue;

        await command.ExecuteNonQueryAsync(cancellationToken);

        var code = Convert.ToInt32(result.Value);
        if (code < 0)
        {
            throw new InvalidOperationException($"Could not acquire the migration lock (code {code})");
        }
    }

    private static async Task<HashSet<string>> LoadAppliedAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        var applied = new HashSet<string>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand("SELECT name FROM schema_migrations");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            applied.Add(reader.GetString(0));
        }

        return applied;
    }

    private static async Task ApplyAsync(
        SqlConnection connection,
        MigrationScript script,
        CancellationToken cancellationToken)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var command = connection.CreateCommand(script.ReadSql(), transaction))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var record = connection.CreateCommand(
                         "INSERT INTO schema_migrations (name) VALUES (@name)",
                         transaction))
        {
            record.AddParameter("name", SqlDbType.NVarChar, script.Name);
            await record.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ReleaseLockAsync(SqlConnection connection)
    {
        try
        {
            await using var command = connection.CreateCommand("sp_releaseapplock");
            command.CommandType = CommandType.StoredProcedure;
            command.AddParameter("Resource", SqlDbType.NVarChar, LockResource);
            command.AddParameter("LockOwner", SqlDbType.VarChar, "Session");
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }
        catch (Exception error)
        {
            logger.LogWarning(error, "Failed to release the migration lock");
        }
    }
}
