using Conduit.Modules.Articles.Domain;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Articles.Application;

public sealed class DeleteArticle(IArticleRepository articles, IEventBus eventBus, TimeProvider clock)
{
    public async Task ExecuteAsync(Guid requesterId, string slug, CancellationToken cancellationToken = default)
    {
        var article = await articles.FindBySlugAsync(slug, cancellationToken)
                      ?? throw new NotFoundException("article");
        if (!article.IsAuthoredBy(requesterId))
        {
            throw new ForbiddenException("article");
        }

        if (!await articles.DeleteByIdAsync(article.Id, cancellationToken))
        {
            throw new NotFoundException("article");
        }

        await eventBus.PublishAsync(new ArticleDeleted(article.Id, Timestamps.Now(clock)), cancellationToken);
    }
}
