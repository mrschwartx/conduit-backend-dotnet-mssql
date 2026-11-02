using System.Text.RegularExpressions;

namespace Conduit.SharedKernel.Domain;

public sealed partial record Pagination(long Offset, int Limit)
{
    public const int DefaultLimit = 20;

    public const int MaxLimit = 50;

    private const long MaxSafeInteger = 9_007_199_254_740_991;

    public static Pagination Parse(object? offset, object? limit)
    {
        var errors = new ErrorCollector();
        var parsedOffset = ParseInteger(offset, 0, 0, out var offsetError);
        var parsedLimit = ParseInteger(limit, 1, DefaultLimit, out var limitError);

        if (offsetError is not null)
        {
            errors.Add("offset", offsetError);
        }

        if (limitError is not null)
        {
            errors.Add("limit", limitError);
        }

        errors.ThrowIfAny();
        return new Pagination(parsedOffset, (int)Math.Min(parsedLimit, MaxLimit));
    }

    private static long ParseInteger(object? raw, long minimum, long fallback, out string? error)
    {
        error = null;
        if (raw is null)
        {
            return fallback;
        }

        if (raw is not string text || !Digits().IsMatch(TextRules.Trim(text)))
        {
            error = "must be an integer";
            return fallback;
        }

        if (!long.TryParse(TextRules.Trim(text), out var value) || value > MaxSafeInteger || value < minimum)
        {
            error = $"must be at least {minimum}";
            return fallback;
        }

        return value;
    }

    [GeneratedRegex("^[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex Digits();
}
