using System.Text.RegularExpressions;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Domain;
using Microsoft.AspNetCore.Http;

namespace Conduit.SharedKernel.Infrastructure.Http;

public sealed partial class AccessTokenMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IRequestAuthenticator authenticator)
    {
        var metadata = context.GetEndpoint()?.Metadata.GetMetadata<AccessTokenMetadata>();
        if (metadata is not null)
        {
            var user = await ResolveAsync(context, metadata.Mode, authenticator);
            context.SetAuthenticatedUser(user);
        }

        await next(context);
    }

    private static async Task<AuthenticatedUser?> ResolveAsync(
        HttpContext context,
        AccessTokenMode mode,
        IRequestAuthenticator authenticator)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (header.Length == 0)
        {
            return mode == AccessTokenMode.Required ? throw AuthenticationFailedException.MissingToken() : null;
        }

        var match = TokenHeader().Match(header);
        if (!match.Success)
        {
            throw AuthenticationFailedException.InvalidToken();
        }

        return await authenticator.AuthenticateAsync(match.Groups[1].Value, context.RequestAborted);
    }

    [GeneratedRegex(@"^Token (\S+)\z", RegexOptions.CultureInvariant)]
    private static partial Regex TokenHeader();
}
