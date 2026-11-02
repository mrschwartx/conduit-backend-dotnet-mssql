namespace Conduit.Modules.Comments.Domain;

public sealed class Comment
{
    private Comment(
        long id,
        Guid articleId,
        Guid authorId,
        CommentBody body,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        ArticleId = articleId;
        AuthorId = authorId;
        Body = body;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public long Id { get; }

    public Guid ArticleId { get; }

    public Guid AuthorId { get; }

    public CommentBody Body { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }

    public static Comment Draft(Guid articleId, Guid authorId, CommentBody body, DateTimeOffset now) =>
        new(0, articleId, authorId, body, now, now);

    public static Comment Restore(
        long id,
        Guid articleId,
        Guid authorId,
        CommentBody body,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) =>
        new(id, articleId, authorId, body, createdAt, updatedAt);

    public bool IsAuthoredBy(Guid userId) => AuthorId == userId;

    public bool BelongsTo(Guid articleId) => ArticleId == articleId;
}
