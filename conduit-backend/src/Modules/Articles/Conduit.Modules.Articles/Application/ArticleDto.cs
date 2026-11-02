using Conduit.Modules.Articles.Domain;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Articles.Application;

public sealed record ArticleDto(
    string Slug,
    string Title,
    string Description,
    string Body,
    IReadOnlyList<string> TagList,
    string CreatedAt,
    string UpdatedAt,
    bool Favorited,
    int FavoritesCount,
    ProfileDto Author)
{
    public static ArticleDto From(ArticleView view, ProfileDto author) => new(
        view.Slug,
        view.Title,
        view.Description,
        view.Body ?? string.Empty,
        view.TagList,
        Timestamps.Format(view.CreatedAt),
        Timestamps.Format(view.UpdatedAt),
        view.Favorited,
        view.FavoritesCount,
        author);
}

public sealed record ArticleSummaryDto(
    string Slug,
    string Title,
    string Description,
    IReadOnlyList<string> TagList,
    string CreatedAt,
    string UpdatedAt,
    bool Favorited,
    int FavoritesCount,
    ProfileDto Author)
{
    public static ArticleSummaryDto From(ArticleView view, ProfileDto author) => new(
        view.Slug,
        view.Title,
        view.Description,
        view.TagList,
        Timestamps.Format(view.CreatedAt),
        Timestamps.Format(view.UpdatedAt),
        view.Favorited,
        view.FavoritesCount,
        author);
}

public sealed record ArticlePageDto(IReadOnlyList<ArticleSummaryDto> Articles, long ArticlesCount)
{
    public static ArticlePageDto Empty { get; } = new([], 0);

    public static async Task<ArticlePageDto> FromAsync(
        ArticlePage page,
        IAuthorDirectory authors,
        Guid? viewerId,
        CancellationToken cancellationToken)
    {
        var profiles = await authors.FindProfilesAsync(page.Items.Select(item => item.AuthorId), viewerId, cancellationToken);
        return new ArticlePageDto(
            page.Items.Select(item => ArticleSummaryDto.From(item, profiles[item.AuthorId])).ToList(),
            page.Total);
    }
}
