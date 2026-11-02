using System.Globalization;
using System.Text.RegularExpressions;
using Conduit.Modules.Comments.Domain;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Comments.Application;

public sealed partial class DeleteComment(ICommentRepository comments, IArticleCatalog catalog)
{
    private const int MaxDigits = 18;

    public async Task ExecuteAsync(
        Guid requesterId,
        string slug,
        string rawId,
        CancellationToken cancellationToken = default)
    {
        var articleId = await catalog.FindIdBySlugAsync(slug, cancellationToken)
                        ?? throw new NotFoundException("article");

        if (!Digits().IsMatch(rawId))
        {
            throw ValidationException.Field("id", "must be an integer");
        }

        if (rawId.Length > MaxDigits)
        {
            throw new NotFoundException("comment");
        }

        var comment = await comments.FindByIdAsync(long.Parse(rawId, CultureInfo.InvariantCulture), cancellationToken);
        if (comment is null || !comment.BelongsTo(articleId))
        {
            throw new NotFoundException("comment");
        }

        if (!comment.IsAuthoredBy(requesterId))
        {
            throw new ForbiddenException("comment");
        }

        if (!await comments.DeleteByIdAsync(comment.Id, cancellationToken))
        {
            throw new NotFoundException("comment");
        }
    }

    [GeneratedRegex("^[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex Digits();
}
