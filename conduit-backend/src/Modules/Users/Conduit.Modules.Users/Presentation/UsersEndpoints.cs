using Conduit.Modules.Users.Application;
using Conduit.SharedKernel.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Conduit.Modules.Users.Presentation;

public sealed record UserResponse(UserDto User);

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/v1/users/login", async (
            HttpContext context,
            AuthenticateUser authenticateUser,
            CancellationToken cancellationToken) =>
        {
            var input = await RequestBodyReader.ReadEnvelopeAsync(context.Request, "user", cancellationToken);
            return Results.Ok(new UserResponse(await authenticateUser.ExecuteAsync(input, cancellationToken)));
        });

        routes.MapPost("/api/v1/users", async (
            HttpContext context,
            RegisterUser registerUser,
            CancellationToken cancellationToken) =>
        {
            var input = await RequestBodyReader.ReadEnvelopeAsync(context.Request, "user", cancellationToken);
            var user = await registerUser.ExecuteAsync(input, cancellationToken);
            return Results.Json(new UserResponse(user), statusCode: StatusCodes.Status201Created);
        });

        routes.MapGet("/api/v1/user", async (
            HttpContext context,
            GetCurrentUser getCurrentUser,
            CancellationToken cancellationToken) =>
        {
            var requester = context.GetAuthenticatedUser();
            return Results.Ok(new UserResponse(await getCurrentUser.ExecuteAsync(requester.UserId, cancellationToken)));
        }).RequireAccessToken();

        routes.MapPut("/api/v1/user", async (
            HttpContext context,
            UpdateCurrentUser updateCurrentUser,
            CancellationToken cancellationToken) =>
        {
            var requester = context.GetAuthenticatedUser();
            var changes = await RequestBodyReader.ReadEnvelopeAsync(context.Request, "user", cancellationToken);
            var user = await updateCurrentUser.ExecuteAsync(requester.UserId, changes, cancellationToken);
            return Results.Ok(new UserResponse(user));
        }).RequireAccessToken();

        return routes;
    }
}
