using Conduit.Modules.Articles.Application;
using Conduit.SharedKernel.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Conduit.Modules.Articles.Presentation;

public sealed record ArticleResponse(ArticleDto Article);

public sealed record TagsResponse(IReadOnlyList<string> Tags);

public static class ArticlesEndpoints
{
    public static IEndpointRouteBuilder MapArticleEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/articles");

        group.MapGet("/", async (
            HttpContext context,
            ListArticles listArticles,
            CancellationToken cancellationToken) =>
            Results.Ok(await listArticles.ExecuteAsync(
                context.GetViewerId(),
                QueryParameters.All(context.Request.Query),
                cancellationToken)))
            .OptionalAccessToken();

        group.MapGet("/feed", async (
            HttpContext context,
            GetFeed getFeed,
            CancellationToken cancellationToken) =>
            Results.Ok(await getFeed.ExecuteAsync(
                context.GetAuthenticatedUser().UserId,
                QueryParameters.All(context.Request.Query),
                cancellationToken)))
            .RequireAccessToken();

        group.MapGet("/{slug}", async (
            string slug,
            HttpContext context,
            GetArticle getArticle,
            CancellationToken cancellationToken) =>
            Results.Ok(new ArticleResponse(await getArticle.ExecuteAsync(slug, context.GetViewerId(), cancellationToken))))
            .OptionalAccessToken();

        group.MapPost("/", async (
            HttpContext context,
            CreateArticle createArticle,
            CancellationToken cancellationToken) =>
        {
            var requester = context.GetAuthenticatedUser();
            var input = await RequestBodyReader.ReadEnvelopeAsync(context.Request, "article", cancellationToken);
            var article = await createArticle.ExecuteAsync(requester.UserId, input, cancellationToken);
            return Results.Json(new ArticleResponse(article), statusCode: StatusCodes.Status201Created);
        }).RequireAccessToken();

        group.MapPut("/{slug}", async (
            string slug,
            HttpContext context,
            UpdateArticle updateArticle,
            CancellationToken cancellationToken) =>
        {
            var requester = context.GetAuthenticatedUser();
            var changes = await RequestBodyReader.ReadEnvelopeAsync(context.Request, "article", cancellationToken);
            var article = await updateArticle.ExecuteAsync(requester.UserId, slug, changes, cancellationToken);
            return Results.Ok(new ArticleResponse(article));
        }).RequireAccessToken();

        group.MapDelete("/{slug}", async (
            string slug,
            HttpContext context,
            DeleteArticle deleteArticle,
            CancellationToken cancellationToken) =>
        {
            await deleteArticle.ExecuteAsync(context.GetAuthenticatedUser().UserId, slug, cancellationToken);
            return Results.NoContent();
        }).RequireAccessToken();

        group.MapPost("/{slug}/favorite", async (
            string slug,
            HttpContext context,
            SetFavorite setFavorite,
            CancellationToken cancellationToken) =>
            Results.Ok(new ArticleResponse(
                await setFavorite.ExecuteAsync(context.GetAuthenticatedUser().UserId, slug, true, cancellationToken))))
            .RequireAccessToken();

        group.MapDelete("/{slug}/favorite", async (
            string slug,
            HttpContext context,
            SetFavorite setFavorite,
            CancellationToken cancellationToken) =>
            Results.Ok(new ArticleResponse(
                await setFavorite.ExecuteAsync(context.GetAuthenticatedUser().UserId, slug, false, cancellationToken))))
            .RequireAccessToken();

        routes.MapGet("/api/v1/tags", async (ListPopularTags listPopularTags, CancellationToken cancellationToken) =>
            Results.Ok(new TagsResponse(await listPopularTags.ExecuteAsync(cancellationToken))));

        return routes;
    }
}
