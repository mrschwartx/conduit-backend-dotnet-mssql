using Microsoft.Data.SqlClient;

namespace Conduit.SharedKernel.Infrastructure.Persistence;

public static class SqlServerConnectionString
{
    private const int DefaultPort = 1433;

    public static string Build(string source, int poolMax, int timeoutMs)
    {
        var builder = source.Contains("://", StringComparison.Ordinal)
            ? FromUrl(source)
            : new SqlConnectionStringBuilder(source);
        builder.MaxPoolSize = Math.Max(1, poolMax);
        builder.ConnectTimeout = Math.Max(1, (int)Math.Ceiling(timeoutMs / 1000.0));
        return builder.ConnectionString;
    }

    private static SqlConnectionStringBuilder FromUrl(string source)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)
            || (uri.Scheme != "sqlserver" && uri.Scheme != "mssql"))
        {
            throw new ArgumentException(
                "DATABASE_URL must be a sqlserver:// URL or a SQL Server connection string");
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"{uri.Host},{(uri.Port > 0 ? uri.Port : DefaultPort)}"
        };

        var userInfo = uri.UserInfo;
        if (userInfo.Length > 0)
        {
            var separator = userInfo.IndexOf(':');
            builder.UserID = Uri.UnescapeDataString(separator < 0 ? userInfo : userInfo[..separator]);
            if (separator >= 0)
            {
                builder.Password = Uri.UnescapeDataString(userInfo[(separator + 1)..]);
            }
        }

        var database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/'));
        if (database.Length > 0)
        {
            builder.InitialCatalog = database;
        }

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            var key = parts[0];
            var value = Uri.UnescapeDataString(parts[1]);
            if (key.Equals("encrypt", StringComparison.OrdinalIgnoreCase))
            {
                builder.Encrypt = ParseEncrypt(value);
            }
            else if (key.Equals("trustServerCertificate", StringComparison.OrdinalIgnoreCase)
                     && bool.TryParse(value, out var trust))
            {
                builder.TrustServerCertificate = trust;
            }
        }

        return builder;
    }

    private static SqlConnectionEncryptOption ParseEncrypt(string value) =>
        value.ToLowerInvariant() switch
        {
            "true" or "yes" or "mandatory" => SqlConnectionEncryptOption.Mandatory,
            "false" or "no" or "optional" => SqlConnectionEncryptOption.Optional,
            "strict" => SqlConnectionEncryptOption.Strict,
            _ => throw new ArgumentException("encrypt must be true, false or strict")
        };
}
