using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Comments.Application;

public sealed class ArticleDeletedHandler(DeleteCommentsByArticle deleteCommentsByArticle)
    : IIntegrationEventHandler<ArticleDeleted>
{
    public Task HandleAsync(ArticleDeleted integrationEvent, CancellationToken cancellationToken) =>
        deleteCommentsByArticle.ExecuteAsync(integrationEvent.ArticleId, cancellationToken);
}
