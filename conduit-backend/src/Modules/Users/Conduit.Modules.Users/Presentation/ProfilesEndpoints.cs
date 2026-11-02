using Conduit.Modules.Users.Application;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Conduit.Modules.Users.Presentation;

public sealed record ProfileResponse(ProfileDto Profile);

public static class ProfilesEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/profiles");

        group.MapGet("/{username}", async (
            string username,
            HttpContext context,
            GetProfile getProfile,
            CancellationToken cancellationToken) =>
            Results.Ok(new ProfileResponse(
                await getProfile.ExecuteAsync(username, context.GetViewerId(), cancellationToken))))
            .OptionalAccessToken();

        group.MapPost("/{username}/follow", async (
            string username,
            HttpContext context,
            FollowUser followUser,
            CancellationToken cancellationToken) =>
            Results.Ok(new ProfileResponse(
                await followUser.ExecuteAsync(context.GetAuthenticatedUser().UserId, username, cancellationToken))))
            .RequireAccessToken();

        group.MapDelete("/{username}/follow", async (
            string username,
            HttpContext context,
            UnfollowUser unfollowUser,
            CancellationToken cancellationToken) =>
            Results.Ok(new ProfileResponse(
                await unfollowUser.ExecuteAsync(context.GetAuthenticatedUser().UserId, username, cancellationToken))))
            .RequireAccessToken();

        return routes;
    }
}
