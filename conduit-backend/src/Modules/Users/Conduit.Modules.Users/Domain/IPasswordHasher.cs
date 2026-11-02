namespace Conduit.Modules.Users.Domain;

public interface IPasswordHasher
{
    string Hash(string plain);

    bool Matches(string plain, string hash);
}
