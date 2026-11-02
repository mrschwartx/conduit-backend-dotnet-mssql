namespace Conduit.Modules.Users;

public sealed record UsersModuleOptions(string AuthSecret, TimeSpan TokenTtl, int BcryptRounds);
