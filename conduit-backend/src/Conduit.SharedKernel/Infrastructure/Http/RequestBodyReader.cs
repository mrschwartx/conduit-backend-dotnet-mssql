using System.Text.Json;
using Conduit.SharedKernel.Domain;
using Microsoft.AspNetCore.Http;

namespace Conduit.SharedKernel.Infrastructure.Http;

public static class RequestBodyReader
{
    public const int MaxBytes = 256 * 1024;

    private static readonly IReadOnlyDictionary<string, object?> Empty = new Dictionary<string, object?>();

    public static async Task<IReadOnlyDictionary<string, object?>> ReadEnvelopeAsync(
        HttpRequest request,
        string key,
        CancellationToken cancellationToken)
    {
        var body = await ReadJsonAsync(request, cancellationToken);
        return body.GetValueOrDefault(key) is IReadOnlyDictionary<string, object?> inner ? inner : Empty;
    }

    public static async Task<IReadOnlyDictionary<string, object?>> ReadJsonAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasBody(request))
        {
            return Empty;
        }

        if (!IsJson(request.ContentType))
        {
            throw new UnsupportedMediaTypeException();
        }

        if (request.ContentLength is > MaxBytes)
        {
            throw new PayloadTooLargeException();
        }

        var bytes = await ReadBytesAsync(request.Body, cancellationToken);
        return IsBlank(bytes) ? Empty : Parse(bytes);
    }

    public static IReadOnlyDictionary<string, object?> Parse(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.Object => ToObject(document.RootElement),
                JsonValueKind.Array => Empty,
                _ => throw new MalformedBodyException()
            };
        }
        catch (JsonException)
        {
            throw new MalformedBodyException();
        }
    }

    public static bool IsJson(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var separator = contentType.IndexOf(';');
        var mediaType = (separator < 0 ? contentType : contentType[..separator]).Trim().ToLowerInvariant();
        return mediaType == "application/json" || mediaType.EndsWith("+json", StringComparison.Ordinal);
    }

    private static bool HasBody(HttpRequest request) =>
        request.ContentLength is { } length
            ? length > 0
            : request.Headers.ContainsKey("Transfer-Encoding");

    private static async Task<byte[]> ReadBytesAsync(Stream body, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await body.ReadAsync(chunk.AsMemory(), cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxBytes)
            {
                throw new PayloadTooLargeException();
            }
        }

        return buffer.ToArray();
    }

    private static bool IsBlank(byte[] bytes)
    {
        foreach (var value in bytes)
        {
            if (value is not (0x20 or 0x09 or 0x0A or 0x0D))
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, object?> ToObject(JsonElement element)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            values[property.Name] = ToValue(property.Value);
        }

        return values;
    }

    private static object? ToValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Null => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Object => ToObject(element),
        JsonValueKind.Array => element.EnumerateArray().Select(ToValue).ToList(),
        _ => element.Clone()
    };
}
