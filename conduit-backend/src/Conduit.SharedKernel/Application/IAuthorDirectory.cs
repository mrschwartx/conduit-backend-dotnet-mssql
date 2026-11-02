namespace Conduit.SharedKernel.Application;

public interface IAuthorDirectory
{
    Task<IReadOnlyDictionary<Guid, ProfileDto>> FindProfilesAsync(
        IEnumerable<Guid> userIds,
        Guid? viewerId,
        CancellationToken cancellationToken = default);

    Task<Guid?> FindIdByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> ListFollowedIdsAsync(Guid userId, CancellationToken cancellationToken = default);
}
