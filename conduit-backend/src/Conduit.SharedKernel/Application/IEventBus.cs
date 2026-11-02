using Conduit.SharedKernel.Domain;

namespace Conduit.SharedKernel.Application;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;
}
