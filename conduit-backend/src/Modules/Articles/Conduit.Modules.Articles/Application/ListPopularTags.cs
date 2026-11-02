using Conduit.Modules.Articles.Domain;

namespace Conduit.Modules.Articles.Application;

public sealed class ListPopularTags(IArticleRepository articles)
{
    public const int Limit = 20;

    public Task<IReadOnlyList<string>> ExecuteAsync(CancellationToken cancellationToken = default) =>
        articles.PopularTagsAsync(Limit, cancellationToken);
}
