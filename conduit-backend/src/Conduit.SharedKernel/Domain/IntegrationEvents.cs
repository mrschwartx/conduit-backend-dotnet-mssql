namespace Conduit.SharedKernel.Domain;

public interface IIntegrationEvent
{
    DateTimeOffset OccurredAt { get; }
}

public sealed record ArticleDeleted(Guid ArticleId, DateTimeOffset OccurredAt) : IIntegrationEvent;
