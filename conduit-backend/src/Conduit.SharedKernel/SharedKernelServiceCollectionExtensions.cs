using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Infrastructure.Events;
using Conduit.SharedKernel.Infrastructure.Persistence;

namespace Conduit.SharedKernel;

public static class SharedKernelServiceCollectionExtensions
{
    public static IServiceCollection AddSharedKernel(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<NpgsqlDataSource>(_ => NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        services.AddSingleton<SqlMigrator>();
        services.AddHostedService<MigrationHostedService>();
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
        return services;
    }
}
