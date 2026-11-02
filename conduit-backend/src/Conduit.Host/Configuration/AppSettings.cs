using System.Globalization;
using System.Text.RegularExpressions;
using Conduit.Modules.Users;
using Microsoft.Extensions.Logging;

namespace Conduit.Host.Configuration;

public sealed partial record AppSettings(
    string Name,
    string Host,
    int Port,
    string Level,
    string DatabaseUrl,
    int DatabasePoolMax,
    int DatabaseTimeoutMs,
    string AuthSecret,
    TimeSpan TokenTtl,
    int BcryptRounds,
    string ClientUrl,
    LogLevel LogLevel,
    bool SeedAllowProduction)
{
    public const int MinSecretLength = 32;

    public const int MinBcryptRounds = 4;

    public const int MaxBcryptRounds = 31;

    public const string DefaultTokenTtl = "7d";

    public bool IsProduction => Level == "production";

    public static AppSettings From(IConfiguration configuration)
    {
        var level = Text(configuration, "APP_LEVEL") ?? "development";
        var production = level == "production";

        var secret = Required(configuration, "AUTH_SECRET");
        if (secret.Length < MinSecretLength)
        {
            throw new ConfigurationException($"AUTH_SECRET must be at least {MinSecretLength} characters long");
        }

        var rounds = ToInteger(Text(configuration, "BCRYPT_ROUNDS"), 10);
        if (rounds < MinBcryptRounds || rounds > MaxBcryptRounds)
        {
            throw new ConfigurationException($"BCRYPT_ROUNDS must be between {MinBcryptRounds} and {MaxBcryptRounds}");
        }

        return new AppSettings(
            Text(configuration, "APP_NAME") ?? "conduit-api",
            Text(configuration, "APP_HOST") ?? "0.0.0.0",
            ToInteger(Text(configuration, "APP_PORT"), 8080),
            level,
            Required(configuration, "DATABASE_URL"),
            ToInteger(Text(configuration, "DATABASE_POOL_MAX"), 10),
            ToInteger(Text(configuration, "DATABASE_TIMEOUT"), 30000),
            secret,
            ToDuration(Text(configuration, "AUTH_TOKEN_TTL") ?? DefaultTokenTtl),
            rounds,
            (Text(configuration, "CLIENT_URL") ?? "http://localhost:3000").TrimEnd('/'),
            ToLogLevel(Text(configuration, "LOG_LEVEL"), production ? LogLevel.Information : LogLevel.Debug),
            ToBoolean(Text(configuration, "SEED_ALLOW_PRODUCTION"), false));
    }

    public UsersModuleOptions ToUsersOptions() => new(AuthSecret, TokenTtl, BcryptRounds);

    public static TimeSpan ToDuration(string value)
    {
        var match = DurationPattern().Match(value.Trim());
        if (!match.Success)
        {
            throw new ConfigurationException("AUTH_TOKEN_TTL must be a duration such as 3600, 30m, 12h or 7d");
        }

        var amount = long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var seconds = match.Groups[2].Value.ToLowerInvariant() switch
        {
            "m" => amount * 60,
            "h" => amount * 3600,
            "d" => amount * 86400,
            "w" => amount * 604800,
            _ => amount
        };

        if (seconds <= 0 || seconds > 315_360_000)
        {
            throw new ConfigurationException("AUTH_TOKEN_TTL must be a positive duration");
        }

        return TimeSpan.FromSeconds(seconds);
    }

    public static int ToInteger(string? value, int fallback)
    {
        if (value is null)
        {
            return fallback;
        }

        var match = IntegerPrefix().Match(value);
        return match.Success && int.TryParse(match.Value, out var parsed) ? parsed : fallback;
    }

    public static bool ToBoolean(string? value, bool fallback)
    {
        if (string.IsNullOrEmpty(value))
        {
            return fallback;
        }

        return value.ToLowerInvariant() is "1" or "true" or "yes";
    }

    public static LogLevel ToLogLevel(string? value, LogLevel fallback) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "info" or "information" => LogLevel.Information,
            "warn" or "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "critical" or "fatal" => LogLevel.Critical,
            _ => fallback
        };

    private static string? Text(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static string Required(IConfiguration configuration, string key) =>
        Text(configuration, key) ?? throw new ConfigurationException($"Missing required environment variable: {key}");

    [GeneratedRegex(@"^\s*[+-]?\d+", RegexOptions.CultureInvariant)]
    private static partial Regex IntegerPrefix();

    [GeneratedRegex(@"^(\d+)\s*([smhdwSMHDW]?)$", RegexOptions.CultureInvariant)]
    private static partial Regex DurationPattern();
}
