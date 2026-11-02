using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Comments.Domain;

public sealed record CommentBody
{
    public const int MaxLength = 1000;

    private CommentBody(string value) => Value = value;

    public string Value { get; }

    public static CommentBody Create(object? raw) => new(TextRules.Parse("body", raw, MaxLength));

    public static CommentBody Restore(string value) => new(value);
}
