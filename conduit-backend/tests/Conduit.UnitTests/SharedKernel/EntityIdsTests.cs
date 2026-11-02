using Conduit.SharedKernel.Domain;

namespace Conduit.UnitTests.SharedKernel;

public sealed class EntityIdsTests
{
    [Fact]
    public void GeneratesDistinctValidIds()
    {
        var first = EntityIds.Generate();
        var second = EntityIds.Generate();

        Assert.NotEqual(first, second);
        Assert.True(EntityIds.IsValid(first.ToString()));
    }

    [Fact]
    public void AcceptsUppercaseAndNormalizesIt()
    {
        Assert.True(EntityIds.TryParse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA", out var parsed));
        Assert.Equal("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", parsed.ToString());
    }

    [Fact]
    public void RejectsMalformedAndNonStringValues()
    {
        foreach (var value in new object?[] { null, 42, "", "nope", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa", "{aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa}", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" })
        {
            Assert.False(EntityIds.IsValid(value));
        }
    }
}
