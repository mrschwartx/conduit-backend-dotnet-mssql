using Conduit.Modules.Comments.Domain;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Comments.Application;

public sealed class ListComments(ICommentRepository comments, IArticleCatalog catalog, IAuthorDirectory authors)
{
    public async Task<IReadOnlyList<CommentDto>> ExecuteAsync(
        string slug,
        Guid? viewerId,
        CancellationToken cancellationToken = default)
    {
        var articleId = await catalog.FindIdBySlugAsync(slug, cancellationToken)
                        ?? throw new NotFoundException("article");

        var found = await comments.ListByArticleAsync(articleId, cancellationToken);
        var profiles = await authors.FindProfilesAsync(found.Select(comment => comment.AuthorId), viewerId, cancellationToken);
        return found.Select(comment => CommentDto.From(comment, profiles[comment.AuthorId])).ToList();
    }
}
