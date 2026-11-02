using Conduit.Modules.Users.Domain;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Users.Application;

public sealed class GetCurrentUser(IUserRepository users, ITokenService tokens)
{
    public async Task<UserDto> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(userId, cancellationToken)
                   ?? throw AuthenticationFailedException.InvalidToken();
        return UserDto.From(user, tokens.Issue(user.Id));
    }
}
