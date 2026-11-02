using Conduit.Modules.Articles.Domain;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Articles.Application;

public sealed class ListArticles(IArticleRepository articles, IAuthorDirectory authors)
{
    private static readonly string[] Filters = ["tag", "author", "favorited"];

    public async Task<ArticlePageDto> ExecuteAsync(
        Guid? viewerId,
        IReadOnlyDictionary<string, object?> query,
        CancellationToken cancellationToken = default)
    {
        var errors = new ErrorCollector();
        var paging = errors.Capture(() => Pagination.Parse(query.GetValueOrDefault("offset"), query.GetValueOrDefault("limit")));
        var filters = ReadFilters(query, errors);
        errors.ThrowIfAny();

        Guid? authorId = null;
        if (filters.TryGetValue("author", out var author))
        {
            authorId = await authors.FindIdByUsernameAsync(author, cancellationToken);
            if (authorId is null)
            {
                return ArticlePageDto.Empty;
            }
        }

        Guid? favoritedById = null;
        if (filters.TryGetValue("favorited", out var favorited))
        {
            favoritedById = await authors.FindIdByUsernameAsync(favorited, cancellationToken);
            if (favoritedById is null)
            {
                return ArticlePageDto.Empty;
            }
        }

        var page = await articles.FindPageAsync(
            new ArticleQuery(
                filters.GetValueOrDefault("tag"),
                authorId,
                null,
                favoritedById,
                viewerId,
                paging!.Offset,
                paging.Limit),
            cancellationToken);
        return await ArticlePageDto.FromAsync(page, authors, viewerId, cancellationToken);
    }

    private static Dictionary<string, string> ReadFilters(IReadOnlyDictionary<string, object?> query, ErrorCollector errors)
    {
        var filters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in Filters)
        {
            if (!query.TryGetValue(name, out var value) || value is null)
            {
                continue;
            }

            if (value is string text)
            {
                filters[name] = text;
            }
            else
            {
                errors.Add(name, Messages.Invalid);
            }
        }

        return filters;
    }
}
