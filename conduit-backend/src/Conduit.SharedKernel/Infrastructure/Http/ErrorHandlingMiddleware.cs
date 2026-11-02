using Conduit.SharedKernel.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Conduit.SharedKernel.Infrastructure.Http;

public sealed class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public const string ServerErrorMessage = "internal server error";

    private static readonly IReadOnlyDictionary<string, int> StatusByCode = new Dictionary<string, int>
    {
        ["VALIDATION_ERROR"] = StatusCodes.Status422UnprocessableEntity,
        ["CONFLICT"] = StatusCodes.Status409Conflict,
        ["NOT_FOUND"] = StatusCodes.Status404NotFound,
        ["AUTHENTICATION_FAILED"] = StatusCodes.Status401Unauthorized,
        ["FORBIDDEN"] = StatusCodes.Status403Forbidden,
        ["UNSUPPORTED_MEDIA_TYPE"] = StatusCodes.Status415UnsupportedMediaType,
        ["MALFORMED_BODY"] = StatusCodes.Status400BadRequest,
        ["PAYLOAD_TOO_LARGE"] = StatusCodes.Status413PayloadTooLarge
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
        }
        catch (Exception error) when (!context.Response.HasStarted)
        {
            await WriteAsync(context, error);
        }
    }

    private async Task WriteAsync(HttpContext context, Exception error)
    {
        var (status, errors) = Describe(error);
        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(error, "Unhandled error: {Message}", error.Message);
        }
        else
        {
            logger.LogWarning("Request rejected status={Status} message={Message}", status, error.Message);
        }

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ErrorResponse(errors));
    }

    private static (int Status, IReadOnlyDictionary<string, string[]> Errors) Describe(Exception error) => error switch
    {
        DomainException domain => (
            StatusByCode.GetValueOrDefault(domain.Code, StatusCodes.Status500InternalServerError),
            domain.Errors),
        BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } => (
            StatusCodes.Status413PayloadTooLarge,
            new PayloadTooLargeException().Errors),
        BadHttpRequestException bad => (
            bad.StatusCode,
            new Dictionary<string, string[]> { ["request"] = ["is invalid"] }),
        _ => (
            StatusCodes.Status500InternalServerError,
            new Dictionary<string, string[]> { ["server"] = [ServerErrorMessage] })
    };
}
