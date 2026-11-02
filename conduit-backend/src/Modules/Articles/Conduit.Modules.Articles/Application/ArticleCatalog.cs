using Conduit.Modules.Articles.Domain;
using Conduit.SharedKernel.Application;

namespace Conduit.Modules.Articles.Application;

public sealed class ArticleCatalog(IArticleRepository articles) : IArticleCatalog
{
    public async Task<Guid?> FindIdBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var article = await articles.FindBySlugAsync(slug, cancellationToken);
        return article?.Id;
    }
}
