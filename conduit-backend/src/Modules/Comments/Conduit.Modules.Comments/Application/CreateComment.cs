using Conduit.Modules.Comments.Domain;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Comments.Application;

public sealed class CreateComment(
    ICommentRepository comments,
    IArticleCatalog catalog,
    IAuthorDirectory authors,
    TimeProvider clock)
{
    public async Task<CommentDto> ExecuteAsync(
        Guid authorId,
        string slug,
        IReadOnlyDictionary<string, object?> input,
        CancellationToken cancellationToken = default)
    {
        var articleId = await catalog.FindIdBySlugAsync(slug, cancellationToken)
                        ?? throw new NotFoundException("article");
        var body = CommentBody.Create(input.GetValueOrDefault("body"));

        var saved = await comments.AddAsync(Comment.Draft(articleId, authorId, body, Timestamps.Now(clock)), cancellationToken);
        var profiles = await authors.FindProfilesAsync([authorId], authorId, cancellationToken);
        return CommentDto.From(saved, profiles[authorId]);
    }
}
