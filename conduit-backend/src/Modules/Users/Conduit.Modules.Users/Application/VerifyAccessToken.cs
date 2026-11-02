using Conduit.Modules.Users.Domain;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Users.Application;

public sealed class VerifyAccessToken(ITokenService tokens, IUserRepository users)
{
    public async Task<AuthenticatedUser> ExecuteAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(token))
        {
            throw AuthenticationFailedException.MissingToken();
        }

        var userId = await tokens.VerifyAsync(token, cancellationToken);
        if (!await users.ExistsByIdAsync(userId, cancellationToken))
        {
            throw AuthenticationFailedException.InvalidToken();
        }

        return new AuthenticatedUser(userId);
    }
}
