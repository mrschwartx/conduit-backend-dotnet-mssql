using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Conduit.SharedKernel.Infrastructure.Http;

public static class HttpContextExtensions
{
    private const string AuthenticatedUserKey = "Conduit.AuthenticatedUser";

    public static void SetAuthenticatedUser(this HttpContext context, AuthenticatedUser? user)
    {
        if (user is not null)
        {
            context.Items[AuthenticatedUserKey] = user;
        }
    }

    public static AuthenticatedUser GetAuthenticatedUser(this HttpContext context) =>
        context.Items.TryGetValue(AuthenticatedUserKey, out var value) && value is AuthenticatedUser user
            ? user
            : throw AuthenticationFailedException.MissingToken();

    public static Guid? GetViewerId(this HttpContext context) =>
        context.Items.TryGetValue(AuthenticatedUserKey, out var value) && value is AuthenticatedUser user
            ? user.UserId
            : null;

    public static TBuilder RequireAccessToken<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new AccessTokenMetadata(AccessTokenMode.Required));
        builder.AddEndpointFilter(async (invocation, next) =>
        {
            invocation.HttpContext.GetAuthenticatedUser();
            return await next(invocation);
        });
        return builder;
    }

    public static TBuilder OptionalAccessToken<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new AccessTokenMetadata(AccessTokenMode.Optional));
        return builder;
    }
}
