using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Articles.Domain;

public sealed record ArticleTitle
{
    public const int MaxLength = 100;

    private ArticleTitle(string value) => Value = value;

    public string Value { get; }

    public static ArticleTitle Create(object? raw) => new(TextRules.Parse("title", raw, MaxLength));

    public static ArticleTitle Restore(string value) => new(value);
}

public sealed record ArticleDescription
{
    public const int MaxLength = 200;

    private ArticleDescription(string value) => Value = value;

    public string Value { get; }

    public static ArticleDescription Create(object? raw) => new(TextRules.Parse("description", raw, MaxLength));

    public static ArticleDescription Restore(string value) => new(value);
}

public sealed record ArticleBody
{
    public const int MaxLength = 20000;

    private ArticleBody(string value) => Value = value;

    public string Value { get; }

    public static ArticleBody Create(object? raw) => new(TextRules.Parse("body", raw, MaxLength));

    public static ArticleBody Restore(string value) => new(value);
}
