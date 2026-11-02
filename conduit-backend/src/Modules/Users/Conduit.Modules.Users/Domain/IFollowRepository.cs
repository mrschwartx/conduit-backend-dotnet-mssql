namespace Conduit.Modules.Users.Domain;

public interface IFollowRepository
{
    Task FollowAsync(Guid followerId, Guid followedId, CancellationToken cancellationToken = default);

    Task UnfollowAsync(Guid followerId, Guid followedId, CancellationToken cancellationToken = default);

    Task<bool> IsFollowingAsync(Guid followerId, Guid followedId, CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> FindFollowedAmongAsync(
        Guid followerId,
        IReadOnlyCollection<Guid> candidateIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> ListFollowedIdsAsync(Guid followerId, CancellationToken cancellationToken = default);
}
