using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Articles.Domain;

public sealed record TagList
{
    public const int MaxTags = 10;

    public const int MaxTagLength = 30;

    private TagList(IReadOnlyList<string> values) => Values = values;

    public IReadOnlyList<string> Values { get; }

    public static TagList Create(object? raw)
    {
        if (raw is null)
        {
            return new TagList([]);
        }

        if (raw is not IReadOnlyList<object?> entries)
        {
            throw ValidationException.Field("tagList", Messages.Invalid);
        }

        foreach (var entry in entries)
        {
            var problem = Problem(entry);
            if (problem is not null)
            {
                throw ValidationException.Field("tagList", problem);
            }
        }

        var unique = entries.Select(entry => TextRules.Trim((string)entry!)).Distinct(StringComparer.Ordinal).ToList();
        if (unique.Count > MaxTags)
        {
            throw ValidationException.Field("tagList", $"is too long (maximum is {MaxTags} tags)");
        }

        return new TagList(unique);
    }

    public static TagList Restore(IEnumerable<string> values) => new(values.ToList());

    public bool SameAs(TagList other) => Values.SequenceEqual(other.Values, StringComparer.Ordinal);

    private static string? Problem(object? raw)
    {
        if (raw is not string text)
        {
            return Messages.Invalid;
        }

        var tag = TextRules.Trim(text);
        if (tag.Length == 0)
        {
            return "can't contain blank tags";
        }

        if (tag.Any(character => TextRules.IsTrimmable(character) || character == ','))
        {
            return "cannot contain whitespace or commas inside a tag";
        }

        return TextRules.CountCharacters(tag) > MaxTagLength
            ? $"cannot contain a tag longer than {MaxTagLength} characters"
            : null;
    }
}
