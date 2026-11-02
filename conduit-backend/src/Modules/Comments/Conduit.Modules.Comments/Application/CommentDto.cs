using Conduit.Modules.Comments.Domain;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Comments.Application;

public sealed record CommentDto(long Id, string CreatedAt, string UpdatedAt, string Body, ProfileDto Author)
{
    public static CommentDto From(Comment comment, ProfileDto author) => new(
        comment.Id,
        Timestamps.Format(comment.CreatedAt),
        Timestamps.Format(comment.UpdatedAt),
        comment.Body.Value,
        author);
}
