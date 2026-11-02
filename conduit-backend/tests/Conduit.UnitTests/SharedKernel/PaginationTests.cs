using Conduit.SharedKernel.Domain;

namespace Conduit.UnitTests.SharedKernel;

public sealed class PaginationTests
{
    [Fact]
    public void UsesDefaultsWhenAbsent()
    {
        var paging = Pagination.Parse(null, null);

        Assert.Equal(0, paging.Offset);
        Assert.Equal(20, paging.Limit);
    }

    [Fact]
    public void ParsesValidValuesAndCapsTheLimit()
    {
        var paging = Pagination.Parse("40", " 500 ");

        Assert.Equal(40, paging.Offset);
        Assert.Equal(50, paging.Limit);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("1.5")]
    public void RejectsNonIntegers(string raw)
    {
        var error = Assert.Throws<ValidationException>(() => Pagination.Parse(raw, raw));

        Assert.Equal(new[] { "must be an integer" }, error.Errors["offset"]);
        Assert.Equal(new[] { "must be an integer" }, error.Errors["limit"]);
    }

    [Fact]
    public void RejectsRepeatedValues()
    {
        var error = Assert.Throws<ValidationException>(() => Pagination.Parse(new[] { "1", "2" }, null));

        Assert.Equal(new[] { "must be an integer" }, error.Errors["offset"]);
        Assert.False(error.Errors.ContainsKey("limit"));
    }

    [Fact]
    public void RejectsALimitBelowOne()
    {
        var error = Assert.Throws<ValidationException>(() => Pagination.Parse(null, "0"));

        Assert.Equal(new[] { "must be at least 1" }, error.Errors["limit"]);
    }

    [Fact]
    public void RejectsUnsafeIntegers()
    {
        var error = Assert.Throws<ValidationException>(() => Pagination.Parse("9007199254740992", null));

        Assert.Equal(new[] { "must be at least 0" }, error.Errors["offset"]);
    }
}
