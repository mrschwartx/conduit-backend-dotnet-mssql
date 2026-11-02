using System.Text.RegularExpressions;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Users.Domain;

public sealed record Username
{
    public const int MinLength = 3;

    public const int MaxLength = 30;

    private static readonly Regex Pattern = new(
        "^[A-Za-z0-9_-]+$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(250));

    private Username(string value) => Value = value;

    public string Value { get; }

    public static Username Create(object? raw) =>
        new(TextRules.Parse("username", raw, MaxLength, MinLength, Pattern));

    public static Username Restore(string value) => new(value);
}
