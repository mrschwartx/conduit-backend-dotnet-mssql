using Conduit.Modules.Articles.Domain;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Articles.Application;

public sealed class GetFeed(IArticleRepository articles, IAuthorDirectory authors)
{
    public async Task<ArticlePageDto> ExecuteAsync(
        Guid viewerId,
        IReadOnlyDictionary<string, object?> query,
        CancellationToken cancellationToken = default)
    {
        var paging = Pagination.Parse(query.GetValueOrDefault("offset"), query.GetValueOrDefault("limit"));
        var authorIds = await authors.ListFollowedIdsAsync(viewerId, cancellationToken);
        if (authorIds.Count == 0)
        {
            return ArticlePageDto.Empty;
        }

        var page = await articles.FindPageAsync(
            new ArticleQuery(null, null, authorIds, null, viewerId, paging.Offset, paging.Limit),
            cancellationToken);
        return await ArticlePageDto.FromAsync(page, authors, viewerId, cancellationToken);
    }
}
