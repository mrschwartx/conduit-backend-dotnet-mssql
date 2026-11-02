using Conduit.Modules.Users.Domain;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Users.Application;

public sealed class UnfollowUser(IUserRepository users, IFollowRepository follows)
{
    public async Task<ProfileDto> ExecuteAsync(
        Guid followerId,
        string username,
        CancellationToken cancellationToken = default)
    {
        var target = await users.FindByUsernameAsync(username, cancellationToken)
                     ?? throw new NotFoundException("profile");
        await follows.UnfollowAsync(followerId, target.Id, cancellationToken);
        return ProfileMapper.ToProfile(target, false);
    }
}
