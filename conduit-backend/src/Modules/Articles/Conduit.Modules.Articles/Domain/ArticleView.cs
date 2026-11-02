namespace Conduit.Modules.Articles.Domain;

public sealed record ArticleView(
    Guid Id,
    Guid AuthorId,
    string Slug,
    string Title,
    string Description,
    string? Body,
    IReadOnlyList<string> TagList,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int FavoritesCount,
    bool Favorited);

public sealed record ArticlePage(IReadOnlyList<ArticleView> Items, long Total);

public sealed record ArticleQuery(
    string? Tag,
    Guid? AuthorId,
    IReadOnlyCollection<Guid>? AuthorIds,
    Guid? FavoritedById,
    Guid? ViewerId,
    long Offset,
    int Limit);
