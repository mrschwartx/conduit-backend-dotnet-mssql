using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Conduit.Modules.Articles.Domain;

public static partial class Slug
{
    public const int MaxLength = 80;

    public const string Fallback = "article";

    private static readonly string[] Reserved = ["feed"];

    public static string From(string title)
    {
        var builder = new StringBuilder();
        foreach (var character in title.Normalize(NormalizationForm.FormKD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        var collapsed = TrimDashes(NonAlphanumeric().Replace(builder.ToString(), "-"));
        var shortened = TrimDashes(collapsed.Length > MaxLength ? collapsed[..MaxLength] : collapsed);
        return shortened.Length == 0 ? Fallback : shortened;
    }

    public static string Candidate(string baseSlug, int attempt)
    {
        if (attempt == 1)
        {
            return baseSlug;
        }

        var suffix = $"-{attempt}";
        var room = Math.Max(0, MaxLength - suffix.Length);
        var stem = baseSlug.Length > room ? baseSlug[..room] : baseSlug;
        return TrimDashes(stem) + suffix;
    }

    public static bool IsReserved(string slug) => Reserved.Contains(slug, StringComparer.Ordinal);

    private static string TrimDashes(string value) => value.Trim('-');

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonAlphanumeric();
}
