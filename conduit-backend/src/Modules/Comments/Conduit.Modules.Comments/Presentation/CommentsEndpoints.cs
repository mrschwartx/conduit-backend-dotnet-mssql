using Conduit.Modules.Comments.Application;
using Conduit.SharedKernel.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Conduit.Modules.Comments.Presentation;

public sealed record CommentResponse(CommentDto Comment);

public sealed record CommentsResponse(IReadOnlyList<CommentDto> Comments);

public static class CommentsEndpoints
{
    public static IEndpointRouteBuilder MapCommentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/articles/{slug}/comments");

        group.MapGet("/", async (
            string slug,
            HttpContext context,
            ListComments listComments,
            CancellationToken cancellationToken) =>
            Results.Ok(new CommentsResponse(await listComments.ExecuteAsync(slug, context.GetViewerId(), cancellationToken))))
            .OptionalAccessToken();

        group.MapPost("/", async (
            string slug,
            HttpContext context,
            CreateComment createComment,
            CancellationToken cancellationToken) =>
        {
            var requester = context.GetAuthenticatedUser();
            var input = await RequestBodyReader.ReadEnvelopeAsync(context.Request, "comment", cancellationToken);
            var comment = await createComment.ExecuteAsync(requester.UserId, slug, input, cancellationToken);
            return Results.Json(new CommentResponse(comment), statusCode: StatusCodes.Status201Created);
        }).RequireAccessToken();

        group.MapDelete("/{id}", async (
            string slug,
            string id,
            HttpContext context,
            DeleteComment deleteComment,
            CancellationToken cancellationToken) =>
        {
            await deleteComment.ExecuteAsync(context.GetAuthenticatedUser().UserId, slug, id, cancellationToken);
            return Results.NoContent();
        }).RequireAccessToken();

        return routes;
    }
}
