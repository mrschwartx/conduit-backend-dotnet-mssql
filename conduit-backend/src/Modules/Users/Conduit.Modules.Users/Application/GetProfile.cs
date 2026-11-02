using Conduit.Modules.Users.Domain;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Users.Application;

public sealed class GetProfile(IUserRepository users, IFollowRepository follows)
{
    public async Task<ProfileDto> ExecuteAsync(
        string username,
        Guid? viewerId,
        CancellationToken cancellationToken = default)
    {
        var user = await users.FindByUsernameAsync(username, cancellationToken)
                   ?? throw new NotFoundException("profile");
        var following = viewerId is { } viewer && await follows.IsFollowingAsync(viewer, user.Id, cancellationToken);
        return ProfileMapper.ToProfile(user, following);
    }
}
