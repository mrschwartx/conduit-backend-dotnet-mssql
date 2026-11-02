using Conduit.Modules.Articles.Domain;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Articles.Application;

public sealed class CreateArticle(
    IArticleRepository articles,
    ArticleSlugs slugs,
    IAuthorDirectory authors,
    TimeProvider clock)
{
    public async Task<ArticleDto> ExecuteAsync(
        Guid authorId,
        IReadOnlyDictionary<string, object?> input,
        CancellationToken cancellationToken = default)
    {
        var fields = ArticleInput.Parse(input, ArticleInput.AllFields);
        var id = articles.NextId();

        var article = await ArticleSlugs.PersistAsync(
            () => slugs.AllocateAsync(fields.Title!.Value, null, cancellationToken),
            async slug =>
            {
                var candidate = Article.Create(
                    id,
                    authorId,
                    slug,
                    fields.Title!,
                    fields.Description!,
                    fields.Body!,
                    fields.TagList!,
                    Timestamps.Now(clock));
                await articles.AddAsync(candidate, cancellationToken);
                return candidate;
            });

        var view = await articles.FindViewBySlugAsync(article.Slug, authorId, cancellationToken)
                   ?? throw new NotFoundException("article");
        var profiles = await authors.FindProfilesAsync([authorId], authorId, cancellationToken);
        return ArticleDto.From(view, profiles[authorId]);
    }
}
