using Conduit.Modules.Users.Domain;

namespace Conduit.Modules.Users.Infrastructure;

public sealed class BcryptPasswordHasher(int rounds) : IPasswordHasher
{
    public string Hash(string plain) => BCrypt.Net.BCrypt.HashPassword(plain, rounds);

    public bool Matches(string plain, string hash) => BCrypt.Net.BCrypt.Verify(plain, hash);
}
