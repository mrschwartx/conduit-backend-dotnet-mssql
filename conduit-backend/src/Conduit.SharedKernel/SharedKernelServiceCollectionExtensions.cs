using Microsoft.Extensions.DependencyInjection;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Infrastructure.Events;
using Conduit.SharedKernel.Infrastructure.Persistence;

namespace Conduit.SharedKernel;

public static class SharedKernelServiceCollectionExtensions
{
    public static IServiceCollection AddSharedKernel(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new SqlServerDataSource(connectionString));
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        services.AddSingleton<SqlMigrator>();
        services.AddHostedService<MigrationHostedService>();
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
        return services;
    }
}
