using Conduit.Modules.Articles.Domain;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Articles.Application;

public sealed class SetFavorite(IArticleRepository articles, IAuthorDirectory authors)
{
    public async Task<ArticleDto> ExecuteAsync(
        Guid userId,
        string slug,
        bool favorite,
        CancellationToken cancellationToken = default)
    {
        var article = await articles.FindBySlugAsync(slug, cancellationToken)
                      ?? throw new NotFoundException("article");

        if (favorite)
        {
            await articles.FavoriteAsync(article.Id, userId, cancellationToken);
        }
        else
        {
            await articles.UnfavoriteAsync(article.Id, userId, cancellationToken);
        }

        var view = await articles.FindViewBySlugAsync(slug, userId, cancellationToken)
                   ?? throw new NotFoundException("article");
        var profiles = await authors.FindProfilesAsync([article.AuthorId], userId, cancellationToken);
        return ArticleDto.From(view, profiles[article.AuthorId]);
    }
}
