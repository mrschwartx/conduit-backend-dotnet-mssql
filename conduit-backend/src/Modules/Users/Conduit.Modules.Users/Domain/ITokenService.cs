namespace Conduit.Modules.Users.Domain;

public interface ITokenService
{
    string Issue(Guid userId);

    Task<Guid> VerifyAsync(string token, CancellationToken cancellationToken = default);
}
