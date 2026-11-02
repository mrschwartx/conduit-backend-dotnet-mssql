using Conduit.Modules.Users.Domain;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Users.Application;

public sealed class UpdateCurrentUser(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenService tokens,
    TimeProvider clock)
{
    private static readonly string[] Fields = ["username", "email", "password", "bio", "image"];

    public async Task<UserDto> ExecuteAsync(
        Guid userId,
        IReadOnlyDictionary<string, object?> changes,
        CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(userId, cancellationToken)
                   ?? throw AuthenticationFailedException.InvalidToken();

        if (!Fields.Any(changes.ContainsKey))
        {
            throw ValidationException.Field("user", Messages.Empty);
        }

        var errors = new ErrorCollector();
        var username = changes.ContainsKey("username")
            ? errors.Capture(() => Username.Create(changes["username"]))
            : null;
        var email = changes.ContainsKey("email")
            ? errors.Capture(() => Email.Create(changes["email"]))
            : null;
        var password = changes.ContainsKey("password")
            ? errors.Capture(() => PlainPassword.Create(changes["password"]))
            : null;
        var bio = changes.ContainsKey("bio")
            ? Optional<Bio>.Of(errors.Capture(() => Bio.Create(changes["bio"])))
            : Optional<Bio>.Unset;
        var image = changes.ContainsKey("image")
            ? Optional<ProfileImage>.Of(errors.Capture(() => ProfileImage.Create(changes["image"])))
            : Optional<ProfileImage>.Unset;
        errors.ThrowIfAny();

        if (username is not null || email is not null)
        {
            var taken = await users.FindTakenFieldsAsync(email?.Value, username?.Value, user.Id, cancellationToken);
            if (taken.Count > 0)
            {
                throw ConflictException.Fields(taken);
            }
        }

        user.UpdateProfile(
            username,
            email,
            password is null ? null : passwordHasher.Hash(password.Value),
            bio,
            image,
            Timestamps.Now(clock));

        if (!await users.UpdateAsync(user, cancellationToken))
        {
            throw AuthenticationFailedException.InvalidToken();
        }

        return UserDto.From(user, tokens.Issue(user.Id));
    }
}
