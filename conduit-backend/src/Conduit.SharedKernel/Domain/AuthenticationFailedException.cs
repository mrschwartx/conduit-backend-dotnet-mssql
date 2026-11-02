namespace Conduit.SharedKernel.Domain;

public sealed class AuthenticationFailedException : DomainException
{
    private AuthenticationFailedException(string key, string message)
        : base(message, "AUTHENTICATION_FAILED", new Dictionary<string, string[]> { [key] = [message] })
    {
        Key = key;
    }

    public string Key { get; }

    public static AuthenticationFailedException MissingToken() => new("token", "is missing");

    public static AuthenticationFailedException InvalidToken() => new("token", "is invalid");

    public static AuthenticationFailedException ExpiredToken() => new("token", "has expired");

    public static AuthenticationFailedException InvalidCredentials() => new("credentials", "are invalid");
}
