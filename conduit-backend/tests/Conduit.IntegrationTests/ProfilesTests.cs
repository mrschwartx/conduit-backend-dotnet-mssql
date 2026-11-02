using Conduit.IntegrationTests.Support;

namespace Conduit.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ProfilesTests(ApiFixture api)
{
    [Fact]
    public async Task ShowsAProfileToAnonymousVisitors()
    {
        var user = await api.RegisterAsync("pub");

        var response = await api.CreateClient().GetAsync($"/api/v1/profiles/{user.Username.ToUpperInvariant()}");

        Assert.Equal(200, response.Status);
        var profile = response.Property("profile");
        Assert.Equal(user.Username, profile.GetProperty("username").GetString());
        Assert.False(profile.GetProperty("following").GetBoolean());
        Assert.False(response.Body.Contains("email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FollowsAndUnfollowsIdempotently()
    {
        var follower = await api.RegisterAsync("fol");
        var target = await api.RegisterAsync("tgt");

        var first = await follower.Client.PostEmptyAsync($"/api/v1/profiles/{target.Username}/follow");
        var second = await follower.Client.PostEmptyAsync($"/api/v1/profiles/{target.Username}/follow");
        var seen = await follower.Client.GetAsync($"/api/v1/profiles/{target.Username}");
        var removed = await follower.Client.DeleteAsync($"/api/v1/profiles/{target.Username}/follow");
        var again = await follower.Client.DeleteAsync($"/api/v1/profiles/{target.Username}/follow");

        Assert.True(first.Property("profile").GetProperty("following").GetBoolean());
        Assert.Equal(200, second.Status);
        Assert.True(seen.Property("profile").GetProperty("following").GetBoolean());
        Assert.False(removed.Property("profile").GetProperty("following").GetBoolean());
        Assert.Equal(200, again.Status);
    }

    [Fact]
    public async Task RejectsFollowingYourselfWith422()
    {
        var user = await api.RegisterAsync("self");

        var response = await user.Client.PostEmptyAsync($"/api/v1/profiles/{user.Username}/follow");

        Assert.Equal(422, response.Status);
        Assert.Equal("you cannot follow yourself", response.ErrorMessage("username"));
    }

    [Fact]
    public async Task ChecksAuthenticationBeforeExistence()
    {
        var user = await api.RegisterAsync("chk");

        var anonymous = await api.CreateClient().PostEmptyAsync("/api/v1/profiles/ghost_user/follow");
        var missing = await user.Client.PostEmptyAsync("/api/v1/profiles/ghost_user/follow");
        var unknown = await api.CreateClient().GetAsync("/api/v1/profiles/ghost_user");

        Assert.Equal(401, anonymous.Status);
        Assert.Equal(404, missing.Status);
        Assert.Equal("not found", missing.ErrorMessage("profile"));
        Assert.Equal(404, unknown.Status);
    }

    [Fact]
    public async Task RejectsAnInvalidTokenEvenOnPublicProfiles()
    {
        var user = await api.RegisterAsync("inv");
        var client = api.CreateClient();
        client.Token = "garbage";

        var response = await client.GetAsync($"/api/v1/profiles/{user.Username}");

        Assert.Equal(401, response.Status);
    }
}
