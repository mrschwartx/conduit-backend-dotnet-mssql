using Conduit.Modules.Users.Domain;
using Conduit.SharedKernel.Application;

namespace Conduit.Modules.Users.Application;

public sealed class AuthorDirectory(IUserRepository users, IFollowRepository follows) : IAuthorDirectory
{
    public async Task<IReadOnlyDictionary<Guid, ProfileDto>> FindProfilesAsync(
        IEnumerable<Guid> userIds,
        Guid? viewerId,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, ProfileDto>();
        }

        var found = await users.FindByIdsAsync(ids, cancellationToken);
        IReadOnlySet<Guid> followed = viewerId is { } viewer
            ? await follows.FindFollowedAmongAsync(viewer, ids, cancellationToken)
            : new HashSet<Guid>();

        return found.ToDictionary(user => user.Id, user => ProfileMapper.ToProfile(user, followed.Contains(user.Id)));
    }

    public async Task<Guid?> FindIdByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByUsernameAsync(username, cancellationToken);
        return user?.Id;
    }

    public Task<IReadOnlyList<Guid>> ListFollowedIdsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        follows.ListFollowedIdsAsync(userId, cancellationToken);
}
