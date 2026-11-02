using System.Text;
using System.Text.Json;

namespace Conduit.IntegrationTests.Support;

public sealed class ApiClient(HttpClient http)
{
    public string? Token { get; set; }

    public static string Json(params (string Key, object? Value)[] pairs) =>
        JsonSerializer.Serialize(pairs.ToDictionary(pair => pair.Key, pair => pair.Value));

    public static string Envelope(string key, params (string Key, object? Value)[] pairs) =>
        JsonSerializer.Serialize(new Dictionary<string, object?> { [key] = pairs.ToDictionary(pair => pair.Key, pair => pair.Value) });

    public Task<ApiResponse> GetAsync(string path, IDictionary<string, string>? headers = null) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Get, path), headers);

    public Task<ApiResponse> DeleteAsync(string path) => SendAsync(new HttpRequestMessage(HttpMethod.Delete, path), null);

    public Task<ApiResponse> OptionsAsync(string path, IDictionary<string, string> headers) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Options, path), headers);

    public Task<ApiResponse> PostAsync(string path, string json) => SendJsonAsync(HttpMethod.Post, path, json);

    public Task<ApiResponse> PutAsync(string path, string json) => SendJsonAsync(HttpMethod.Put, path, json);

    public Task<ApiResponse> PostEmptyAsync(string path) => SendAsync(new HttpRequestMessage(HttpMethod.Post, path), null);

    public Task<ApiResponse> SendRawAsync(HttpMethod method, string path, string body, string contentType) =>
        SendAsync(new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, contentType) }, null);

    private Task<ApiResponse> SendJsonAsync(HttpMethod method, string path, string json) =>
        SendRawAsync(method, path, json, "application/json");

    private async Task<ApiResponse> SendAsync(HttpRequestMessage request, IDictionary<string, string>? headers)
    {
        if (headers is not null)
        {
            foreach (var (name, value) in headers)
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }
        }

        if (Token is not null && !request.Headers.Contains("Authorization"))
        {
            request.Headers.TryAddWithoutValidation("Authorization", $"Token {Token}");
        }

        using var response = await http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        var collected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            collected[header.Key] = string.Join(", ", header.Value);
        }

        return new ApiResponse((int)response.StatusCode, body, collected);
    }
}
