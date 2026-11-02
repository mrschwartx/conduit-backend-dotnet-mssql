using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Domain;

namespace Conduit.SharedKernel.Infrastructure.Events;

public sealed class InMemoryEventBus(IServiceProvider services, ILogger<InMemoryEventBus> logger) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        foreach (var handler in services.GetServices<IIntegrationEventHandler<TEvent>>())
        {
            try
            {
                await handler.HandleAsync(integrationEvent, cancellationToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogError(error, "Event handler failed for event {EventName}", typeof(TEvent).Name);
            }
        }
    }
}
