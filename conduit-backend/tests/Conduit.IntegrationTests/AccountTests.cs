using Conduit.IntegrationTests.Support;

namespace Conduit.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AccountTests(ApiFixture api)
{
    private static string NewUser(string username, string email, string password = ApiFixture.Password) =>
        ApiClient.Envelope("user", ("username", username), ("email", email), ("password", password));

    [Fact]
    public async Task RegistersAUserAndReturnsTheUserEnvelope()
    {
        var client = api.CreateClient();
        var username = "reg_" + Guid.NewGuid().ToString("N")[..8];

        var response = await client.PostAsync("/api/v1/users", NewUser(username, $"{username}@mail.com"));

        Assert.Equal(201, response.Status);
        var user = response.Property("user");
        Assert.Equal(username, user.GetProperty("username").GetString());
        Assert.Equal($"{username}@mail.com", user.GetProperty("email").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, user.GetProperty("bio").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, user.GetProperty("image").ValueKind);
        Assert.False(string.IsNullOrEmpty(user.GetProperty("token").GetString()));
        Assert.False(response.Body.Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RejectsDuplicateEmailAndUsernameWith409()
    {
        var existing = await api.RegisterAsync("dup");

        var response = await api.CreateClient().PostAsync(
            "/api/v1/users",
            NewUser(existing.Username.ToUpperInvariant(), existing.Email.ToUpperInvariant()));

        Assert.Equal(409, response.Status);
        Assert.Equal("has already been taken", response.ErrorMessage("email"));
        Assert.Equal("has already been taken", response.ErrorMessage("username"));
    }

    [Fact]
    public async Task ReportsEveryInvalidRegistrationFieldWith422()
    {
        var response = await api.CreateClient().PostAsync("/api/v1/users", NewUser("a", "nope", "short"));

        Assert.Equal(422, response.Status);
        Assert.Equal(new[] { "username", "email", "password" }, response.ErrorFields().ToArray());
        Assert.Equal("is too short (minimum is 3 characters)", response.ErrorMessage("username"));
        Assert.Equal("is invalid", response.ErrorMessage("email"));
        Assert.Equal("is too short (minimum is 8 characters)", response.ErrorMessage("password"));
    }

    [Fact]
    public async Task TreatsAMissingEnvelopeAsBlankFields()
    {
        var response = await api.CreateClient().PostAsync("/api/v1/users", "{}");

        Assert.Equal(422, response.Status);
        Assert.Equal("can't be blank", response.ErrorMessage("username"));
    }

    [Fact]
    public async Task LogsInWithEmailAndPassword()
    {
        var user = await api.RegisterAsync("login");

        var response = await api.CreateClient().PostAsync(
            "/api/v1/users/login",
            ApiClient.Envelope("user", ("email", user.Email.ToUpperInvariant()), ("password", user.Password)));

        Assert.Equal(200, response.Status);
        Assert.Equal(user.Username, response.Property("user").GetProperty("username").GetString());
        Assert.False(string.IsNullOrEmpty(response.Property("user").GetProperty("token").GetString()));
    }

    [Fact]
    public async Task RejectsBadCredentialsWith401AndBlankFieldsWith422()
    {
        var user = await api.RegisterAsync("badlogin");
        var client = api.CreateClient();

        var wrong = await client.PostAsync(
            "/api/v1/users/login",
            ApiClient.Envelope("user", ("email", user.Email), ("password", "wrong-password")));
        var unknown = await client.PostAsync(
            "/api/v1/users/login",
            ApiClient.Envelope("user", ("email", "ghost@mail.com"), ("password", "whatever123")));
        var blank = await client.PostAsync("/api/v1/users/login", ApiClient.Envelope("user", ("email", " "), ("password", "")));

        Assert.Equal(401, wrong.Status);
        Assert.Equal("are invalid", wrong.ErrorMessage("credentials"));
        Assert.Equal(401, unknown.Status);
        Assert.Equal(wrong.Body, unknown.Body);
        Assert.Equal(422, blank.Status);
        Assert.Equal("can't be blank", blank.ErrorMessage("email"));
        Assert.Equal("can't be blank", blank.ErrorMessage("password"));
    }

    [Fact]
    public async Task ReturnsTheCurrentUser()
    {
        var user = await api.RegisterAsync("me");

        var response = await user.Client.GetAsync("/api/v1/user");

        Assert.Equal(200, response.Status);
        Assert.Equal(user.Email, response.Property("user").GetProperty("email").GetString());
    }

    [Fact]
    public async Task RejectsMissingMalformedAndForgedTokensWith401()
    {
        var client = api.CreateClient();

        var missing = await client.GetAsync("/api/v1/user");
        var bearer = await client.GetAsync("/api/v1/user", new Dictionary<string, string> { ["Authorization"] = "Bearer abc" });
        var garbage = await client.GetAsync("/api/v1/user", new Dictionary<string, string> { ["Authorization"] = "Token garbage" });

        Assert.Equal(401, missing.Status);
        Assert.Equal("is missing", missing.ErrorMessage("token"));
        Assert.Equal(401, bearer.Status);
        Assert.Equal("is invalid", bearer.ErrorMessage("token"));
        Assert.Equal(401, garbage.Status);
        Assert.Equal("is invalid", garbage.ErrorMessage("token"));
    }

    [Fact]
    public async Task UpdatesTheCurrentUser()
    {
        var user = await api.RegisterAsync("upd");

        var response = await user.Client.PutAsync(
            "/api/v1/user",
            ApiClient.Envelope("user", ("bio", "I like to skateboard"), ("image", "https://i.stack.imgur.com/xHWG8.jpg")));

        Assert.Equal(200, response.Status);
        Assert.Equal("I like to skateboard", response.Property("user").GetProperty("bio").GetString());
        Assert.Equal("https://i.stack.imgur.com/xHWG8.jpg", response.Property("user").GetProperty("image").GetString());
        Assert.Equal(user.Username, response.Property("user").GetProperty("username").GetString());
    }

    [Fact]
    public async Task ClearsBioAndImageWithNull()
    {
        var user = await api.RegisterAsync("clr");
        await user.Client.PutAsync("/api/v1/user", ApiClient.Envelope("user", ("bio", "bio"), ("image", "https://a.co/i.png")));

        var response = await user.Client.PutAsync("/api/v1/user", ApiClient.Envelope("user", ("bio", null), ("image", null)));

        Assert.Equal(200, response.Status);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, response.Property("user").GetProperty("bio").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, response.Property("user").GetProperty("image").ValueKind);
    }

    [Fact]
    public async Task ChangesThePasswordAndAcceptsTheNewOneAtLogin()
    {
        var user = await api.RegisterAsync("pwd");

        var update = await user.Client.PutAsync("/api/v1/user", ApiClient.Envelope("user", ("password", "BrandNewPass1!")));
        var oldLogin = await api.CreateClient().PostAsync(
            "/api/v1/users/login",
            ApiClient.Envelope("user", ("email", user.Email), ("password", user.Password)));
        var newLogin = await api.CreateClient().PostAsync(
            "/api/v1/users/login",
            ApiClient.Envelope("user", ("email", user.Email), ("password", "BrandNewPass1!")));

        Assert.Equal(200, update.Status);
        Assert.Equal(401, oldLogin.Status);
        Assert.Equal(200, newLogin.Status);
    }

    [Fact]
    public async Task RejectsEmptyAndInvalidUpdatesWith422AndTakenCredentialsWith409()
    {
        var user = await api.RegisterAsync("inv");
        var other = await api.RegisterAsync("other");

        var empty = await user.Client.PutAsync("/api/v1/user", ApiClient.Envelope("user"));
        var invalid = await user.Client.PutAsync(
            "/api/v1/user",
            ApiClient.Envelope("user", ("image", "http://insecure.example/a.png"), ("bio", new string('a', 301))));
        var taken = await user.Client.PutAsync("/api/v1/user", ApiClient.Envelope("user", ("email", other.Email)));

        Assert.Equal(422, empty.Status);
        Assert.Equal("can't be empty", empty.ErrorMessage("user"));
        Assert.Equal(422, invalid.Status);
        Assert.Equal("must be a valid https URL", invalid.ErrorMessage("image"));
        Assert.Equal("is too long (maximum is 300 characters)", invalid.ErrorMessage("bio"));
        Assert.Equal(409, taken.Status);
        Assert.Equal("has already been taken", taken.ErrorMessage("email"));
    }

    [Fact]
    public async Task RequiresAuthenticationToUpdate()
    {
        var response = await api.CreateClient().PutAsync("/api/v1/user", ApiClient.Envelope("user", ("bio", "x")));

        Assert.Equal(401, response.Status);
    }
}
