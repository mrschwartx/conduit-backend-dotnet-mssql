using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Users.Domain;

public sealed record ProfileImage
{
    public const int MaxLength = 2048;

    private ProfileImage(string value) => Value = value;

    public string Value { get; }

    public static ProfileImage? Create(object? raw)
    {
        if (raw is null)
        {
            return null;
        }

        if (raw is not string text)
        {
            throw ValidationException.Field("image", Messages.Invalid);
        }

        var value = TextRules.Trim(text);
        if (value.Length == 0)
        {
            return null;
        }

        if (TextRules.CountCharacters(value) > MaxLength)
        {
            throw ValidationException.Field("image", Messages.TooLong(MaxLength));
        }

        if (!IsHttpsUrl(value))
        {
            throw ValidationException.Field("image", "must be a valid https URL");
        }

        return new ProfileImage(value);
    }

    public static ProfileImage? Restore(string? value) => value is null ? null : new ProfileImage(value);

    private static bool IsHttpsUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.Host.Length > 0;
}
