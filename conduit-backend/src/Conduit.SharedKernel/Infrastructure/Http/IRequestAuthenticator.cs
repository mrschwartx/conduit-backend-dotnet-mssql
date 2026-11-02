using Conduit.SharedKernel.Application;

namespace Conduit.SharedKernel.Infrastructure.Http;

public interface IRequestAuthenticator
{
    Task<AuthenticatedUser> AuthenticateAsync(string token, CancellationToken cancellationToken);
}
