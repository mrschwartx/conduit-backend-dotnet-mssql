using Conduit.Modules.Users.Domain;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Users.Application;

public sealed class FollowUser(IUserRepository users, IFollowRepository follows)
{
    public async Task<ProfileDto> ExecuteAsync(
        Guid followerId,
        string username,
        CancellationToken cancellationToken = default)
    {
        var target = await users.FindByUsernameAsync(username, cancellationToken)
                     ?? throw new NotFoundException("profile");
        if (target.Id == followerId)
        {
            throw ValidationException.Field("username", "you cannot follow yourself");
        }

        await follows.FollowAsync(followerId, target.Id, cancellationToken);
        return ProfileMapper.ToProfile(target, true);
    }
}
