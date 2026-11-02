namespace Conduit.Modules.Articles.Domain;

public interface IArticleRepository
{
    Guid NextId();

    Task AddAsync(Article article, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(Article article, CancellationToken cancellationToken = default);

    Task<Article?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<ArticleView?> FindViewBySlugAsync(string slug, Guid? viewerId, CancellationToken cancellationToken = default);

    Task<ArticlePage> FindPageAsync(ArticleQuery query, CancellationToken cancellationToken = default);

    Task<bool> SlugTakenAsync(string slug, Guid? excludeId, CancellationToken cancellationToken = default);

    Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task FavoriteAsync(Guid articleId, Guid userId, CancellationToken cancellationToken = default);

    Task UnfavoriteAsync(Guid articleId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> PopularTagsAsync(int limit, CancellationToken cancellationToken = default);
}
