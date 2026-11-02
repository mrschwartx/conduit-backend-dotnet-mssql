using Npgsql;
using Conduit.SharedKernel.Infrastructure.Persistence;

namespace Conduit.UnitTests.SharedKernel;

public sealed class PostgresConnectionStringTests
{
    [Fact]
    public void ParsesPostgresUrls()
    {
        var builder = new NpgsqlConnectionStringBuilder(
            PostgresConnectionString.Build("postgres://dev_user:dev_pass@db.local:6543/conduit_dev", 7, 20000));

        Assert.Equal("db.local", builder.Host);
        Assert.Equal(6543, builder.Port);
        Assert.Equal("dev_user", builder.Username);
        Assert.Equal("dev_pass", builder.Password);
        Assert.Equal("conduit_dev", builder.Database);
        Assert.Equal(7, builder.MaxPoolSize);
        Assert.Equal(20, builder.Timeout);
    }

    [Fact]
    public void DefaultsThePortAndDecodesCredentials()
    {
        var builder = new NpgsqlConnectionStringBuilder(
            PostgresConnectionString.Build("postgresql://us%40er:p%2Fss@localhost/app", 10, 500));

        Assert.Equal(5432, builder.Port);
        Assert.Equal("us@er", builder.Username);
        Assert.Equal("p/ss", builder.Password);
        Assert.Equal(1, builder.Timeout);
    }

    [Fact]
    public void ReadsTheSslModeFromTheQueryString()
    {
        var builder = new NpgsqlConnectionStringBuilder(
            PostgresConnectionString.Build("postgres://u:p@h/db?sslmode=verify-full", 10, 5000));

        Assert.Equal(SslMode.VerifyFull, builder.SslMode);
    }

    [Fact]
    public void AcceptsKeywordConnectionStrings()
    {
        var builder = new NpgsqlConnectionStringBuilder(
            PostgresConnectionString.Build("Host=localhost;Database=conduit;Username=u;Password=p", 3, 3000));

        Assert.Equal("localhost", builder.Host);
        Assert.Equal(3, builder.MaxPoolSize);
        Assert.Equal(3, builder.Timeout);
    }

    [Fact]
    public void RejectsUnsupportedUrls()
    {
        Assert.Throws<ArgumentException>(() => PostgresConnectionString.Build("mysql://u:p@h/db", 10, 5000));
    }
}
