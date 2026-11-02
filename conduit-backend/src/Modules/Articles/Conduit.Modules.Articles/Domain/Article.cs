namespace Conduit.Modules.Articles.Domain;

public sealed class Article
{
    private Article(
        Guid id,
        Guid authorId,
        string slug,
        ArticleTitle title,
        ArticleDescription description,
        ArticleBody body,
        TagList tagList,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        AuthorId = authorId;
        Slug = slug;
        Title = title;
        Description = description;
        Body = body;
        TagList = tagList;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }

    public Guid AuthorId { get; }

    public string Slug { get; private set; }

    public ArticleTitle Title { get; private set; }

    public ArticleDescription Description { get; private set; }

    public ArticleBody Body { get; private set; }

    public TagList TagList { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Article Create(
        Guid id,
        Guid authorId,
        string slug,
        ArticleTitle title,
        ArticleDescription description,
        ArticleBody body,
        TagList tagList,
        DateTimeOffset now) =>
        new(id, authorId, slug, title, description, body, tagList, now, now);

    public static Article Restore(
        Guid id,
        Guid authorId,
        string slug,
        ArticleTitle title,
        ArticleDescription description,
        ArticleBody body,
        TagList tagList,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) =>
        new(id, authorId, slug, title, description, body, tagList, createdAt, updatedAt);

    public bool IsAuthoredBy(Guid userId) => AuthorId == userId;

    public void Revise(
        string slug,
        ArticleTitle? title,
        ArticleDescription? description,
        ArticleBody? body,
        TagList? tagList,
        DateTimeOffset now)
    {
        Slug = slug;
        Title = title ?? Title;
        Description = description ?? Description;
        Body = body ?? Body;
        TagList = tagList ?? TagList;
        UpdatedAt = now;
    }
}
