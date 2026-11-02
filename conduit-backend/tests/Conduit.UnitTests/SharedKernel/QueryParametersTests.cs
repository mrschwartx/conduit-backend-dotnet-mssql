using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Conduit.SharedKernel.Infrastructure.Http;

namespace Conduit.UnitTests.SharedKernel;

public sealed class QueryParametersTests
{
    [Fact]
    public void ReturnsNullForMissingKeys()
    {
        var query = new QueryCollection();

        Assert.Null(QueryParameters.Value(query, "page"));
    }

    [Fact]
    public void ReturnsSingleValuesAsStrings()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues> { ["page"] = "2" });

        Assert.Equal("2", QueryParameters.Value(query, "page"));
    }

    [Fact]
    public void ReturnsRepeatedValuesAsArrays()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues> { ["status"] = new[] { "a", "b" } });

        var value = Assert.IsType<string[]>(QueryParameters.Value(query, "status"));
        Assert.Equal(new[] { "a", "b" }, value);
    }
}
