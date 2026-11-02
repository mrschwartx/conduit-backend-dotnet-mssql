using System.Text;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Users.Domain;

public sealed record PlainPassword
{
    public const int MinLength = 8;

    public const int MaxLength = 72;

    public const int MaxBytes = 72;

    private PlainPassword(string value) => Value = value;

    public string Value { get; }

    public static PlainPassword Create(object? raw)
    {
        if (raw is not string { Length: > 0 } text)
        {
            throw ValidationException.Field("password", Messages.Blank);
        }

        var length = TextRules.CountCharacters(text);
        if (length < MinLength)
        {
            throw ValidationException.Field("password", Messages.TooShort(MinLength));
        }

        if (length > MaxLength)
        {
            throw ValidationException.Field("password", Messages.TooLong(MaxLength));
        }

        if (Encoding.UTF8.GetByteCount(text) > MaxBytes)
        {
            throw ValidationException.Field("password", $"is too long (maximum is {MaxBytes} bytes)");
        }

        return new PlainPassword(text);
    }

    public override string ToString() => "PlainPassword[value=***]";
}
