using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Users.Domain;

public sealed record Bio
{
    public const int MaxLength = 300;

    private Bio(string value) => Value = value;

    public string Value { get; }

    public static Bio? Create(object? raw)
    {
        if (raw is null)
        {
            return null;
        }

        if (raw is not string text)
        {
            throw ValidationException.Field("bio", Messages.Invalid);
        }

        var value = TextRules.Trim(text);
        if (value.Length == 0)
        {
            return null;
        }

        if (TextRules.CountCharacters(value) > MaxLength)
        {
            throw ValidationException.Field("bio", Messages.TooLong(MaxLength));
        }

        return new Bio(value);
    }

    public static Bio? Restore(string? value) => value is null ? null : new Bio(value);
}
