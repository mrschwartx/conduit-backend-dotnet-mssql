using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Domain;
using Conduit.SharedKernel.Infrastructure.Http;
using Microsoft.AspNetCore.Http;

namespace Conduit.UnitTests.SharedKernel;

public sealed class AccessTokenMiddlewareTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task SkipsEndpointsWithoutAccessRules()
    {
        var (context, reached) = await RunAsync(null, header: "Token broken");

        Assert.True(reached);
        Assert.Null(context.GetViewerId());
    }

    [Fact]
    public async Task RequiresATokenWhenTheEndpointDemandsOne()
    {
        var error = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => RunAsync(AccessTokenMode.Required, header: null));

        Assert.Equal("is missing", error.Errors["token"][0]);
    }

    [Fact]
    public async Task LetsAnonymousRequestsThroughOnOptionalEndpoints()
    {
        var (context, reached) = await RunAsync(AccessTokenMode.Optional, header: null);

        Assert.True(reached);
        Assert.Null(context.GetViewerId());
    }

    [Theory]
    [InlineData("Bearer abc")]
    [InlineData("Token")]
    [InlineData("Token ")]
    [InlineData("Token a b")]
    [InlineData("token abc")]
    public async Task RejectsMalformedHeadersEvenOnOptionalEndpoints(string header)
    {
        var error = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => RunAsync(AccessTokenMode.Optional, header));

        Assert.Equal("is invalid", error.Errors["token"][0]);
    }

    [Fact]
    public async Task ExposesTheAuthenticatedUser()
    {
        var (context, reached) = await RunAsync(AccessTokenMode.Required, "Token good");

        Assert.True(reached);
        Assert.Equal(UserId, context.GetViewerId());
        Assert.Equal(UserId, context.GetAuthenticatedUser().UserId);
    }

    [Fact]
    public async Task PropagatesAuthenticatorFailures()
    {
        var error = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => RunAsync(AccessTokenMode.Optional, "Token bad"));

        Assert.Equal("has expired", error.Errors["token"][0]);
    }

    private static async Task<(HttpContext Context, bool Reached)> RunAsync(AccessTokenMode? mode, string? header)
    {
        var context = new DefaultHttpContext();
        if (header is not null)
        {
            context.Request.Headers.Authorization = header;
        }

        if (mode is { } value)
        {
            context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new AccessTokenMetadata(value)), "test"));
        }

        var reached = false;
        var middleware = new AccessTokenMiddleware(_ =>
        {
            reached = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, new StubAuthenticator());
        return (context, reached);
    }

    private sealed class StubAuthenticator : IRequestAuthenticator
    {
        public Task<AuthenticatedUser> AuthenticateAsync(string token, CancellationToken cancellationToken) =>
            token switch
            {
                "good" => Task.FromResult(new AuthenticatedUser(UserId)),
                "bad" => throw AuthenticationFailedException.ExpiredToken(),
                _ => throw AuthenticationFailedException.InvalidToken()
            };
    }
}
