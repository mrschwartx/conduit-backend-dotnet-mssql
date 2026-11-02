using Npgsql;

namespace Conduit.SharedKernel.Infrastructure.Persistence;

public static class PostgresConnectionString
{
    private const int DefaultPort = 5432;

    public static string Build(string source, int poolMax, int timeoutMs)
    {
        var builder = source.Contains("://", StringComparison.Ordinal)
            ? FromUrl(source)
            : new NpgsqlConnectionStringBuilder(source);
        builder.MaxPoolSize = Math.Max(1, poolMax);
        builder.Timeout = Math.Max(1, (int)Math.Ceiling(timeoutMs / 1000.0));
        return builder.ConnectionString;
    }

    private static NpgsqlConnectionStringBuilder FromUrl(string source)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)
            || (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
        {
            throw new ArgumentException("DATABASE_URL must be a postgres:// URL or an Npgsql connection string");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : DefaultPort
        };

        var userInfo = uri.UserInfo;
        if (userInfo.Length > 0)
        {
            var separator = userInfo.IndexOf(':');
            builder.Username = Uri.UnescapeDataString(separator < 0 ? userInfo : userInfo[..separator]);
            if (separator >= 0)
            {
                builder.Password = Uri.UnescapeDataString(userInfo[(separator + 1)..]);
            }
        }

        var database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/'));
        if (database.Length > 0)
        {
            builder.Database = database;
        }

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2
                && parts[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<SslMode>(parts[1].Replace("-", string.Empty), true, out var mode))
            {
                builder.SslMode = mode;
            }
        }

        return builder;
    }
}
