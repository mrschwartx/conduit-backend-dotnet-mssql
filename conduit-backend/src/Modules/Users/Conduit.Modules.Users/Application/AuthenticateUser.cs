using Conduit.Modules.Users.Domain;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Users.Application;

public sealed class AuthenticateUser(IUserRepository users, IPasswordHasher passwordHasher, ITokenService tokens)
{
    private const string TimingHash = "$2b$10$la9MsO03GrXB6wPVIxJHf.AWuYIX2h8xZbkFpc8VuCbg2atOt7OEe";

    public async Task<UserDto> ExecuteAsync(
        IReadOnlyDictionary<string, object?> input,
        CancellationToken cancellationToken = default)
    {
        var email = input.GetValueOrDefault("email") as string;
        var password = input.GetValueOrDefault("password") as string;

        var errors = new ErrorCollector();
        if (email is null || TextRules.Trim(email).Length == 0)
        {
            errors.Add("email", Messages.Blank);
        }

        if (string.IsNullOrEmpty(password))
        {
            errors.Add("password", Messages.Blank);
        }

        errors.ThrowIfAny();

        var user = await users.FindByEmailAsync(TextRules.Trim(email!), cancellationToken);
        var matches = passwordHasher.Matches(password!, user?.PasswordHash ?? TimingHash);
        if (user is null || !matches)
        {
            throw AuthenticationFailedException.InvalidCredentials();
        }

        return UserDto.From(user, tokens.Issue(user.Id));
    }
}
