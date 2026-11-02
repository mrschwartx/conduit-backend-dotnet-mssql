using System.Text.Json;
using Conduit.SharedKernel.Domain;
using Conduit.SharedKernel.Infrastructure.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Conduit.UnitTests.SharedKernel;

public sealed class ErrorHandlingMiddlewareTests
{
    [Fact]
    public async Task MapsValidationErrorsTo422WithFieldMessages()
    {
        var (status, body) = await RunAsync(ValidationException.Field("title", "can't be blank"));

        Assert.Equal(422, status);
        Assert.Equal("can't be blank", body.GetProperty("errors").GetProperty("title")[0].GetString());
    }

    [Fact]
    public async Task MapsConflictsTo409()
    {
        var (status, body) = await RunAsync(ConflictException.Fields(["email", "username"]));

        Assert.Equal(409, status);
        Assert.Equal("has already been taken", body.GetProperty("errors").GetProperty("email")[0].GetString());
        Assert.Equal("has already been taken", body.GetProperty("errors").GetProperty("username")[0].GetString());
    }

    [Fact]
    public async Task MapsMissingResourcesTo404()
    {
        var (status, body) = await RunAsync(new NotFoundException("article"));

        Assert.Equal(404, status);
        Assert.Equal("not found", body.GetProperty("errors").GetProperty("article")[0].GetString());
    }

    [Fact]
    public async Task MapsForbiddenActionsTo403()
    {
        var (status, body) = await RunAsync(new ForbiddenException("comment"));

        Assert.Equal(403, status);
        Assert.Equal("forbidden", body.GetProperty("errors").GetProperty("comment")[0].GetString());
    }

    [Theory]
    [InlineData("missing", "is missing")]
    [InlineData("invalid", "is invalid")]
    [InlineData("expired", "has expired")]
    public async Task MapsTokenFailuresTo401(string kind, string message)
    {
        DomainException error = kind switch
        {
            "missing" => AuthenticationFailedException.MissingToken(),
            "invalid" => AuthenticationFailedException.InvalidToken(),
            _ => AuthenticationFailedException.ExpiredToken()
        };

        var (status, body) = await RunAsync(error);

        Assert.Equal(401, status);
        Assert.Equal(message, body.GetProperty("errors").GetProperty("token")[0].GetString());
    }

    [Fact]
    public async Task MapsBadCredentialsTo401()
    {
        var (status, body) = await RunAsync(AuthenticationFailedException.InvalidCredentials());

        Assert.Equal(401, status);
        Assert.Equal("are invalid", body.GetProperty("errors").GetProperty("credentials")[0].GetString());
    }

    [Fact]
    public async Task MapsRequestLevelFailures()
    {
        var (unsupported, unsupportedBody) = await RunAsync(new UnsupportedMediaTypeException());
        var (malformed, malformedBody) = await RunAsync(new MalformedBodyException());
        var (large, largeBody) = await RunAsync(new PayloadTooLargeException());

        Assert.Equal(415, unsupported);
        Assert.Equal("must be application/json", unsupportedBody.GetProperty("errors").GetProperty("content-type")[0].GetString());
        Assert.Equal(400, malformed);
        Assert.Equal("is not valid JSON", malformedBody.GetProperty("errors").GetProperty("body")[0].GetString());
        Assert.Equal(413, large);
        Assert.Equal("is too large", largeBody.GetProperty("errors").GetProperty("body")[0].GetString());
    }

    [Fact]
    public async Task HidesUnexpectedErrors()
    {
        var (status, body) = await RunAsync(new InvalidOperationException("secret detail"));

        Assert.Equal(500, status);
        Assert.Equal(ErrorHandlingMiddleware.ServerErrorMessage, body.GetProperty("errors").GetProperty("server")[0].GetString());
        Assert.DoesNotContain("secret", body.ToString());
    }

    [Fact]
    public async Task PassesSuccessfulRequestsThrough()
    {
        var context = new DefaultHttpContext();
        var middleware = new ErrorHandlingMiddleware(
            innerContext =>
            {
                innerContext.Response.StatusCode = 204;
                return Task.CompletedTask;
            },
            NullLogger<ErrorHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(204, context.Response.StatusCode);
    }

    private static async Task<(int Status, JsonElement Body)> RunAsync(Exception error)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ErrorHandlingMiddleware(_ => throw error, NullLogger<ErrorHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return (context.Response.StatusCode, document.RootElement.Clone());
    }
}
