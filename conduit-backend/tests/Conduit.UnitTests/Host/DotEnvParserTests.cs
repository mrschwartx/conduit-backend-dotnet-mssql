using Conduit.Host.Configuration;

namespace Conduit.UnitTests.Host;

public sealed class DotEnvParserTests
{
    [Fact]
    public void ParsesKeyValuePairs()
    {
        var values = DotEnvParser.Parse(["APP_PORT=8080", "  AUTH_SECRET = abc  ", "EMPTY="]);

        Assert.Equal("8080", values["APP_PORT"]);
        Assert.Equal("abc", values["AUTH_SECRET"]);
        Assert.Equal(string.Empty, values["EMPTY"]);
    }

    [Fact]
    public void SkipsBlankLinesCommentsAndInvalidLines()
    {
        var values = DotEnvParser.Parse(["", "# note", "no-separator", "=value"]);

        Assert.Empty(values);
    }

    [Fact]
    public void StripsQuotesAndExportPrefixes()
    {
        var values = DotEnvParser.Parse(["export A=\"quoted value\"", "B='single'", "C=a=b"]);

        Assert.Equal("quoted value", values["A"]);
        Assert.Equal("single", values["B"]);
        Assert.Equal("a=b", values["C"]);
    }
}
