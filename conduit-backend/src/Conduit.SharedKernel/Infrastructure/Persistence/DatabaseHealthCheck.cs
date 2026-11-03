using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Conduit.SharedKernel.Infrastructure.Persistence;

public sealed class DatabaseHealthCheck(SqlServerDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception error)
        {
            return HealthCheckResult.Unhealthy("Database unavailable", error);
        }
    }
}
