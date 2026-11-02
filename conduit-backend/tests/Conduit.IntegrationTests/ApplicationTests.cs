using System.Text;
using Conduit.IntegrationTests.Support;

namespace Conduit.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ApplicationTests(ApiFixture api)
{
    [Fact]
    public async Task ReportsHealthAsPlainText()
    {
        var response = await api.CreateClient().GetAsync("/health");

        Assert.Equal(200, response.Status);
        Assert.Equal("OK", response.Body);
        Assert.StartsWith("text/plain", response.Header("Content-Type"));
    }

    [Fact]
    public async Task ReturnsAJsonErrorForUnknownPaths()
    {
        var response = await api.CreateClient().GetAsync("/api/v1/does-not-exist");

        Assert.Equal(404, response.Status);
        Assert.Equal("not found", response.ErrorMessage("path"));
        Assert.StartsWith("application/json", response.Header("Content-Type"));
    }

    [Fact]
    public async Task RejectsNonJsonBodiesWith415()
    {
        var response = await api.CreateClient().SendRawAsync(HttpMethod.Post, "/api/v1/users/login", "a=b", "application/x-www-form-urlencoded");

        Assert.Equal(415, response.Status);
        Assert.Equal("must be application/json", response.ErrorMessage("content-type"));
    }

    [Fact]
    public async Task RejectsMalformedJsonWith400()
    {
        var response = await api.CreateClient().SendRawAsync(HttpMethod.Post, "/api/v1/users/login", "{not json", "application/json");

        Assert.Equal(400, response.Status);
        Assert.Equal("is not valid JSON", response.ErrorMessage("body"));
    }

    [Fact]
    public async Task RejectsOversizedBodiesWith413()
    {
        var body = "{\"user\":{\"bio\":\"" + new string('x', 300 * 1024) + "\"}}";

        var response = await api.CreateClient().SendRawAsync(HttpMethod.Post, "/api/v1/users/login", body, "application/json");

        Assert.Equal(413, response.Status);
        Assert.Equal("is too large", response.ErrorMessage("body"));
    }

    [Fact]
    public async Task AcceptsBodylessRequestsWithoutAContentType()
    {
        var user = await api.RegisterAsync("nobody");
        var target = await api.RegisterAsync("nobodytarget");

        var response = await user.Client.PostEmptyAsync($"/api/v1/profiles/{target.Username}/follow");

        Assert.Equal(200, response.Status);
    }

    [Fact]
    public async Task AnswersCorsPreflightForTheConfiguredOrigin()
    {
        var response = await api.CreateClient().OptionsAsync(
            "/api/v1/articles",
            new Dictionary<string, string>
            {
                ["Origin"] = ApiFixture.ClientOrigin,
                ["Access-Control-Request-Method"] = "POST",
                ["Access-Control-Request-Headers"] = "authorization,content-type"
            });

        Assert.True(response.Status is 200 or 204);
        Assert.Equal(ApiFixture.ClientOrigin, response.Header("Access-Control-Allow-Origin"));
        Assert.Contains("Authorization", response.Header("Access-Control-Allow-Headers") ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Null(response.Header("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task OmitsCorsHeadersForOtherOrigins()
    {
        var response = await api.CreateClient().GetAsync(
            "/health",
            new Dictionary<string, string> { ["Origin"] = "http://evil.example" });

        Assert.Null(response.Header("Access-Control-Allow-Origin"));
    }
}
