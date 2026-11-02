using System.Text.Json;

namespace Conduit.IntegrationTests.Support;

public sealed class ApiResponse(int status, string body, IReadOnlyDictionary<string, string> headers)
{
    public int Status { get; } = status;

    public string Body { get; } = body;

    public JsonElement Json => JsonDocument.Parse(Body).RootElement;

    public string? Header(string name) => headers.TryGetValue(name, out var value) ? value : null;

    public JsonElement Property(string name) => Json.GetProperty(name);

    public string? ErrorMessage(string field) => Json.GetProperty("errors").GetProperty(field)[0].GetString();

    public IEnumerable<string> ErrorFields() => Json.GetProperty("errors").EnumerateObject().Select(property => property.Name);
}
