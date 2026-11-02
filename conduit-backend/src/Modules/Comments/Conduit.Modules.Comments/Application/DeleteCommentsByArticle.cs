using Conduit.Modules.Comments.Domain;

namespace Conduit.Modules.Comments.Application;

public sealed class DeleteCommentsByArticle(ICommentRepository comments)
{
    public Task ExecuteAsync(Guid articleId, CancellationToken cancellationToken = default) =>
        comments.DeleteByArticleAsync(articleId, cancellationToken);
}
