using Conduit.Modules.Users.Domain;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Users.Application;

public sealed class RegisterUser(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenService tokens,
    TimeProvider clock)
{
    public async Task<UserDto> ExecuteAsync(
        IReadOnlyDictionary<string, object?> input,
        CancellationToken cancellationToken = default)
    {
        var errors = new ErrorCollector();
        var username = errors.Capture(() => Username.Create(input.GetValueOrDefault("username")));
        var email = errors.Capture(() => Email.Create(input.GetValueOrDefault("email")));
        var password = errors.Capture(() => PlainPassword.Create(input.GetValueOrDefault("password")));
        errors.ThrowIfAny();

        var taken = await users.FindTakenFieldsAsync(email!.Value, username!.Value, null, cancellationToken);
        if (taken.Count > 0)
        {
            throw ConflictException.Fields(taken);
        }

        var user = User.Register(
            users.NextId(),
            username,
            email,
            passwordHasher.Hash(password!.Value),
            Timestamps.Now(clock));
        await users.AddAsync(user, cancellationToken);

        return UserDto.From(user, tokens.Issue(user.Id));
    }
}
