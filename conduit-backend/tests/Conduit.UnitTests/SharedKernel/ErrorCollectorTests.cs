using Conduit.SharedKernel.Domain;

namespace Conduit.UnitTests.SharedKernel;

public sealed class ErrorCollectorTests
{
    [Fact]
    public void ThrowsNothingWhenEmpty()
    {
        new ErrorCollector().ThrowIfAny();
    }

    [Fact]
    public void CapturesFailuresFromEveryField()
    {
        var errors = new ErrorCollector();

        var title = errors.Capture(() => TextRules.Parse("title", null, 10));
        var body = errors.Capture(() => TextRules.Parse("body", "ok", 10));
        var exception = Assert.Throws<ValidationException>(errors.ThrowIfAny);

        Assert.Null(title);
        Assert.Equal("ok", body);
        Assert.Equal(new[] { "title" }, exception.Errors.Keys.ToArray());
    }

    [Fact]
    public void MergesMessagesForTheSameField()
    {
        var errors = new ErrorCollector();
        errors.Add("tagList", "is invalid");
        errors.Add("tagList", "can't contain blank tags");

        var exception = Assert.Throws<ValidationException>(errors.ThrowIfAny);

        Assert.Equal(2, exception.Errors["tagList"].Length);
    }
}
