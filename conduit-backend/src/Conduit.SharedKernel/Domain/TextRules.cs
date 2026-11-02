using System.Text.RegularExpressions;

namespace Conduit.SharedKernel.Domain;

public static class TextRules
{
    public static string Trim(string value)
    {
        var start = 0;
        var end = value.Length;
        while (start < end && IsTrimmable(value[start]))
        {
            start++;
        }

        while (end > start && IsTrimmable(value[end - 1]))
        {
            end--;
        }

        return value.Substring(start, end - start);
    }

    public static bool IsTrimmable(char character) =>
        char.IsWhiteSpace(character) || char.IsSeparator(character) || character == '\uFEFF';

    public static int CountCharacters(string value)
    {
        var count = 0;
        foreach (var _ in value.EnumerateRunes())
        {
            count++;
        }

        return count;
    }

    public static string Parse(
        string field,
        object? raw,
        int maxLength,
        int minLength = 1,
        Regex? pattern = null,
        string patternMessage = Messages.Invalid)
    {
        if (raw is not string text)
        {
            throw ValidationException.Field(field, Messages.Blank);
        }

        var value = Trim(text);
        var length = CountCharacters(value);
        if (length == 0 && minLength > 0)
        {
            throw ValidationException.Field(field, Messages.Blank);
        }

        if (length < minLength)
        {
            throw ValidationException.Field(field, Messages.TooShort(minLength));
        }

        if (length > maxLength)
        {
            throw ValidationException.Field(field, Messages.TooLong(maxLength));
        }

        if (pattern is not null && !pattern.IsMatch(value))
        {
            throw ValidationException.Field(field, patternMessage);
        }

        return value;
    }
}
