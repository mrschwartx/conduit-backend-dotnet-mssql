using Conduit.SharedKernel.Domain;

namespace Conduit.SharedKernel.Application;

public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}
