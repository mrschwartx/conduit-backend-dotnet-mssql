namespace Conduit.IntegrationTests.Support;

public sealed record RegisteredUser(ApiClient Client, string Username, string Email, string Password, string Token);

public sealed class ApiFixture : IAsyncLifetime
{
    public const string Password = "Password123!";

    public const string ClientOrigin = "http://localhost:3000";

    public const string AuthSecret = "integration-test-secret-integration-test-secret";

    private TestDatabase? database;

    private ConduitApiFactory? factory;

    public async Task InitializeAsync()
    {
        database = await TestDatabase.CreateAsync();

        Environment.SetEnvironmentVariable("DATABASE_URL", database.ConnectionString);
        Environment.SetEnvironmentVariable("AUTH_SECRET", AuthSecret);
        Environment.SetEnvironmentVariable("BCRYPT_ROUNDS", "4");
        Environment.SetEnvironmentVariable("CLIENT_URL", ClientOrigin);
        Environment.SetEnvironmentVariable("APP_LEVEL", "development");
        Environment.SetEnvironmentVariable("LOG_LEVEL", "warning");

        factory = new ConduitApiFactory();
        factory.CreateClient().Dispose();
    }

    public async Task DisposeAsync()
    {
        factory?.Dispose();
        if (database is not null)
        {
            await database.DisposeAsync();
        }
    }

    public ApiClient CreateClient() => factory!.CreateApiClient();

    public async Task<RegisteredUser> RegisterAsync(string? prefix = null)
    {
        var client = CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var username = $"{prefix ?? "user"}_{suffix}";
        var email = $"{username}@mail.com";

        var response = await client.PostAsync(
            "/api/v1/users",
            ApiClient.Envelope("user", ("username", username), ("email", email), ("password", Password)));
        Assert.Equal(201, response.Status);

        var token = response.Property("user").GetProperty("token").GetString()!;
        client.Token = token;
        return new RegisteredUser(client, username, email, Password, token);
    }

    public static string ArticleJson(
        string title = "How to train your dragon",
        string description = "Ever wonder how?",
        string body = "You have to believe",
        params string[] tags) =>
        ApiClient.Envelope("article", ("title", title), ("description", description), ("body", body), ("tagList", tags));
}

[CollectionDefinition(ApiCollection.Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}
