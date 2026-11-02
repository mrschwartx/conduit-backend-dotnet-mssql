using System.Text.RegularExpressions;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Users.Domain;

public sealed record Email
{
    public const int MaxLength = 254;

    private static readonly Regex Pattern = new(
        @"^[^\s@]+@[^\s@]+\.[^\s@]+\z",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(250));

    private Email(string value) => Value = value;

    public string Value { get; }

    public static Email Create(object? raw) =>
        new(TextRules.Parse("email", raw, MaxLength, pattern: Pattern).ToLowerInvariant());

    public static Email Restore(string value) => new(value);
}
