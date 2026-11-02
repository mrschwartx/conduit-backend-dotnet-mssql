using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Conduit.SharedKernel.Infrastructure.Persistence;

public sealed class SqlMigrator(
    NpgsqlDataSource dataSource,
    IEnumerable<IMigrationSource> sources,
    ILogger<SqlMigrator> logger)
{
    private const long AdvisoryLockKey = 727001;

    private const string CreateMigrationsTable =
        "CREATE TABLE IF NOT EXISTS schema_migrations (name TEXT PRIMARY KEY, applied_at TIMESTAMPTZ NOT NULL DEFAULT now())";

    public async Task<IReadOnlyList<string>> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var scripts = CollectScripts();
        var executed = new List<string>();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await RunAsync(connection, $"SELECT pg_advisory_lock({AdvisoryLockKey})", cancellationToken);
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
            await UnlockAsync(connection);
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

    private static async Task RunAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<HashSet<string>> LoadAppliedAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var applied = new HashSet<string>(StringComparer.Ordinal);
        await using var command = new NpgsqlCommand("SELECT name FROM schema_migrations", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            applied.Add(reader.GetString(0));
        }

        return applied;
    }

    private static async Task ApplyAsync(
        NpgsqlConnection connection,
        MigrationScript script,
        CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var command = new NpgsqlCommand(script.ReadSql(), connection, transaction))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var record = new NpgsqlCommand(
                         "INSERT INTO schema_migrations (name) VALUES (@name)",
                         connection,
                         transaction))
        {
            record.AddParameter("name", NpgsqlDbType.Text, script.Name);
            await record.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task UnlockAsync(NpgsqlConnection connection)
    {
        try
        {
            await RunAsync(connection, $"SELECT pg_advisory_unlock({AdvisoryLockKey})", CancellationToken.None);
        }
        catch (Exception error)
        {
            logger.LogWarning(error, "Failed to release the migration lock");
        }
    }
}
