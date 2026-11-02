using Conduit.Modules.Users.Domain;
using Conduit.SharedKernel.Application;

namespace Conduit.Modules.Users.Application;

internal static class ProfileMapper
{
    public static ProfileDto ToProfile(User user, bool following) => new(
        user.Username.Value,
        user.Bio?.Value,
        user.Image?.Value,
        following);
}
