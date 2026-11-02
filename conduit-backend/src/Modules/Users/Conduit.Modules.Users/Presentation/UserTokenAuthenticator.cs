using Conduit.Modules.Users.Application;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Infrastructure.Http;

namespace Conduit.Modules.Users.Presentation;

public sealed class UserTokenAuthenticator(VerifyAccessToken verifyAccessToken) : IRequestAuthenticator
{
    public Task<AuthenticatedUser> AuthenticateAsync(string token, CancellationToken cancellationToken) =>
        verifyAccessToken.ExecuteAsync(token, cancellationToken);
}
