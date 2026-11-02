using Conduit.Modules.Articles.Domain;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Articles.Application;

public sealed class GetArticle(IArticleRepository articles, IAuthorDirectory authors)
{
    public async Task<ArticleDto> ExecuteAsync(string slug, Guid? viewerId, CancellationToken cancellationToken = default)
    {
        var view = await articles.FindViewBySlugAsync(slug, viewerId, cancellationToken)
                   ?? throw new NotFoundException("article");
        var profiles = await authors.FindProfilesAsync([view.AuthorId], viewerId, cancellationToken);
        return ArticleDto.From(view, profiles[view.AuthorId]);
    }
}
