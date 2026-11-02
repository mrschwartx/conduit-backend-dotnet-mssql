using Conduit.Modules.Users.Domain;

namespace Conduit.Modules.Users.Application;

public sealed record UserDto(string Email, string Token, string Username, string? Bio, string? Image)
{
    public static UserDto From(User user, string token) => new(
        user.Email.Value,
        token,
        user.Username.Value,
        user.Bio?.Value,
        user.Image?.Value);
}
