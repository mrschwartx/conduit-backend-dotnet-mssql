using Conduit.Modules.Articles.Domain;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Articles.Application;

public sealed class UpdateArticle(
    IArticleRepository articles,
    ArticleSlugs slugs,
    IAuthorDirectory authors,
    TimeProvider clock)
{
    public async Task<ArticleDto> ExecuteAsync(
        Guid requesterId,
        string slug,
        IReadOnlyDictionary<string, object?> changes,
        CancellationToken cancellationToken = default)
    {
        var article = await articles.FindBySlugAsync(slug, cancellationToken)
                      ?? throw new NotFoundException("article");
        if (!article.IsAuthoredBy(requesterId))
        {
            throw new ForbiddenException("article");
        }

        var provided = ArticleInput.Provided(changes);
        if (provided.Count == 0)
        {
            throw ValidationException.Field("article", Messages.Empty);
        }

        var fields = ArticleInput.Parse(changes, provided);
        var titleChanged = fields.Title is not null && fields.Title.Value != article.Title.Value;

        await ArticleSlugs.PersistAsync(
            async () => titleChanged
                ? await slugs.AllocateAsync(fields.Title!.Value, article.Id, cancellationToken)
                : article.Slug,
            async nextSlug =>
            {
                article.Revise(nextSlug, fields.Title, fields.Description, fields.Body, fields.TagList, Timestamps.Now(clock));
                if (!await articles.UpdateAsync(article, cancellationToken))
                {
                    throw new NotFoundException("article");
                }

                return article;
            });

        var view = await articles.FindViewBySlugAsync(article.Slug, requesterId, cancellationToken)
                   ?? throw new NotFoundException("article");
        var profiles = await authors.FindProfilesAsync([article.AuthorId], requesterId, cancellationToken);
        return ArticleDto.From(view, profiles[article.AuthorId]);
    }
}
