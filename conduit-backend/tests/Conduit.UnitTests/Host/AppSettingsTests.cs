using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Conduit.Host.Configuration;

namespace Conduit.UnitTests.Host;

public sealed class AppSettingsTests
{
    private const string Secret = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void AppliesDefaults()
    {
        var settings = AppSettings.From(Configure(("AUTH_SECRET", Secret), ("DATABASE_URL", "postgres://u:p@h/db")));

        Assert.Equal("conduit-api", settings.Name);
        Assert.Equal("0.0.0.0", settings.Host);
        Assert.Equal(8080, settings.Port);
        Assert.Equal("development", settings.Level);
        Assert.False(settings.IsProduction);
        Assert.Equal(10, settings.DatabasePoolMax);
        Assert.Equal(30000, settings.DatabaseTimeoutMs);
        Assert.Equal(10, settings.BcryptRounds);
        Assert.Equal("http://localhost:3000", settings.ClientUrl);
        Assert.Equal(LogLevel.Debug, settings.LogLevel);
        Assert.Equal(TimeSpan.FromDays(7), settings.TokenTtl);
        Assert.False(settings.SeedAllowProduction);
    }

    [Fact]
    public void ReadsOverridesAndProductionDefaults()
    {
        var settings = AppSettings.From(Configure(
            ("AUTH_SECRET", Secret),
            ("DATABASE_URL", "postgres://u:p@h/db"),
            ("APP_LEVEL", "production"),
            ("APP_PORT", "9090"),
            ("BCRYPT_ROUNDS", "12"),
            ("AUTH_TOKEN_TTL", "12h"),
            ("CLIENT_URL", "https://app.example.com/"),
            ("SEED_ALLOW_PRODUCTION", "true")));

        Assert.True(settings.IsProduction);
        Assert.Equal(9090, settings.Port);
        Assert.Equal(12, settings.BcryptRounds);
        Assert.Equal(TimeSpan.FromHours(12), settings.TokenTtl);
        Assert.Equal("https://app.example.com", settings.ClientUrl);
        Assert.Equal(LogLevel.Information, settings.LogLevel);
        Assert.True(settings.SeedAllowProduction);
    }

    [Fact]
    public void RequiresTheAuthSecretAndTheDatabaseUrl()
    {
        var noSecret = Assert.Throws<ConfigurationException>(() =>
            AppSettings.From(Configure(("DATABASE_URL", "postgres://u:p@h/db"))));
        var emptySecret = Assert.Throws<ConfigurationException>(() =>
            AppSettings.From(Configure(("AUTH_SECRET", string.Empty), ("DATABASE_URL", "postgres://u:p@h/db"))));
        var noDatabase = Assert.Throws<ConfigurationException>(() =>
            AppSettings.From(Configure(("AUTH_SECRET", Secret))));

        Assert.Contains("AUTH_SECRET", noSecret.Message);
        Assert.Contains("AUTH_SECRET", emptySecret.Message);
        Assert.Contains("DATABASE_URL", noDatabase.Message);
    }

    [Fact]
    public void RejectsShortSecrets()
    {
        var error = Assert.Throws<ConfigurationException>(() =>
            AppSettings.From(Configure(("AUTH_SECRET", "too-short"), ("DATABASE_URL", "postgres://u:p@h/db"))));

        Assert.Equal("AUTH_SECRET must be at least 32 characters long", error.Message);
    }

    [Fact]
    public void RejectsBcryptRoundsOutsideTheSupportedRange()
    {
        foreach (var rounds in new[] { "3", "32" })
        {
            var error = Assert.Throws<ConfigurationException>(() =>
                AppSettings.From(Configure(
                    ("AUTH_SECRET", Secret),
                    ("DATABASE_URL", "postgres://u:p@h/db"),
                    ("BCRYPT_ROUNDS", rounds))));
            Assert.Equal("BCRYPT_ROUNDS must be between 4 and 31", error.Message);
        }
    }

    [Fact]
    public void BuildsTheUsersModuleOptions()
    {
        var settings = AppSettings.From(Configure(("AUTH_SECRET", Secret), ("DATABASE_URL", "postgres://u:p@h/db")));

        var options = settings.ToUsersOptions();

        Assert.Equal(Secret, options.AuthSecret);
        Assert.Equal(TimeSpan.FromDays(7), options.TokenTtl);
        Assert.Equal(10, options.BcryptRounds);
    }

    [Theory]
    [InlineData("8080", 1, 8080)]
    [InlineData(" 42abc", 1, 42)]
    [InlineData("-5", 1, -5)]
    [InlineData("abc", 7, 7)]
    [InlineData("", 7, 7)]
    public void ParsesLeadingIntegers(string value, int fallback, int expected)
    {
        Assert.Equal(expected, AppSettings.ToInteger(value, fallback));
    }

    [Theory]
    [InlineData("3600", 3600)]
    [InlineData("30m", 1800)]
    [InlineData("12h", 43200)]
    [InlineData("7d", 604800)]
    [InlineData("2w", 1209600)]
    [InlineData("45s", 45)]
    [InlineData(" 7D ", 604800)]
    public void ParsesTokenDurations(string value, int seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), AppSettings.ToDuration(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5m")]
    [InlineData("7 days")]
    [InlineData("99999999999d")]
    public void RejectsInvalidTokenDurations(string value)
    {
        Assert.Throws<ConfigurationException>(() => AppSettings.ToDuration(value));
    }

    [Fact]
    public void ParsesBooleansAndLogLevels()
    {
        Assert.True(AppSettings.ToBoolean("TRUE", false));
        Assert.True(AppSettings.ToBoolean("yes", false));
        Assert.False(AppSettings.ToBoolean("no", true));
        Assert.True(AppSettings.ToBoolean(null, true));
        Assert.Equal(LogLevel.Warning, AppSettings.ToLogLevel("warn", LogLevel.Debug));
        Assert.Equal(LogLevel.Debug, AppSettings.ToLogLevel("unknown", LogLevel.Debug));
    }

    private static IConfiguration Configure(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => (string?)pair.Value))
            .Build();
}
