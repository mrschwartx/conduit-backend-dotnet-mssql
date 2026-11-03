using Microsoft.Data.SqlClient;
using Conduit.SharedKernel.Infrastructure.Persistence;

namespace Conduit.UnitTests.SharedKernel;

public sealed class SqlServerConnectionStringTests
{
    [Fact]
    public void ParsesSqlServerUrls()
    {
        var builder = new SqlConnectionStringBuilder(
            SqlServerConnectionString.Build("sqlserver://dev_user:dev_pass@db.local:14330/conduit_dev", 7, 20000));

        Assert.Equal("db.local,14330", builder.DataSource);
        Assert.Equal("dev_user", builder.UserID);
        Assert.Equal("dev_pass", builder.Password);
        Assert.Equal("conduit_dev", builder.InitialCatalog);
        Assert.Equal(7, builder.MaxPoolSize);
        Assert.Equal(20, builder.ConnectTimeout);
    }

    [Fact]
    public void DefaultsThePortAndDecodesCredentials()
    {
        var builder = new SqlConnectionStringBuilder(
            SqlServerConnectionString.Build("mssql://us%40er:p%2Fss@localhost/app", 10, 500));

        Assert.Equal("localhost,1433", builder.DataSource);
        Assert.Equal("us@er", builder.UserID);
        Assert.Equal("p/ss", builder.Password);
        Assert.Equal(1, builder.ConnectTimeout);
    }

    [Fact]
    public void ReadsTheTlsOptionsFromTheQueryString()
    {
        var builder = new SqlConnectionStringBuilder(
            SqlServerConnectionString.Build("sqlserver://u:p@h/db?encrypt=strict&trustServerCertificate=true", 10, 5000));

        Assert.Equal(SqlConnectionEncryptOption.Strict, builder.Encrypt);
        Assert.True(builder.TrustServerCertificate);
    }

    [Fact]
    public void AcceptsKeywordConnectionStrings()
    {
        var builder = new SqlConnectionStringBuilder(
            SqlServerConnectionString.Build("Server=localhost;Database=conduit;User Id=u;Password=p", 3, 3000));

        Assert.Equal("localhost", builder.DataSource);
        Assert.Equal("conduit", builder.InitialCatalog);
        Assert.Equal(3, builder.MaxPoolSize);
        Assert.Equal(3, builder.ConnectTimeout);
    }

    [Fact]
    public void RejectsUnsupportedUrls()
    {
        Assert.Throws<ArgumentException>(() => SqlServerConnectionString.Build("mysql://u:p@h/db", 10, 5000));
    }

    [Fact]
    public void RejectsUnknownEncryptValues()
    {
        Assert.Throws<ArgumentException>(
            () => SqlServerConnectionString.Build("sqlserver://u:p@h/db?encrypt=sometimes", 10, 5000));
    }
}
