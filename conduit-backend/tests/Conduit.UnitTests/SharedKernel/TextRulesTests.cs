using System.Text.RegularExpressions;
using Conduit.SharedKernel.Domain;

namespace Conduit.UnitTests.SharedKernel;

public sealed class TextRulesTests
{
    [Fact]
    public void TrimsWhitespaceAndByteOrderMarks()
    {
        Assert.Equal("a b", TextRules.Trim("\uFEFF \t a b \n\u00A0"));
        Assert.Equal(string.Empty, TextRules.Trim("  \uFEFF "));
    }

    [Fact]
    public void CountsCodePointsNotUtf16Units()
    {
        Assert.Equal(3, TextRules.CountCharacters("a\U0001F600b"));
        Assert.Equal(0, TextRules.CountCharacters(string.Empty));
    }

    [Fact]
    public void ReturnsTheTrimmedValue()
    {
        Assert.Equal("hello", TextRules.Parse("title", "  hello ", 10));
    }

    [Theory]
    [InlineData(42)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsNonStringAndBlankValues(object raw)
    {
        var error = Assert.Throws<ValidationException>(() => TextRules.Parse("title", raw, 10));

        Assert.Equal(new[] { "can't be blank" }, error.Errors["title"]);
    }

    [Fact]
    public void RejectsMissingValues()
    {
        var error = Assert.Throws<ValidationException>(() => TextRules.Parse("title", null, 10));

        Assert.Equal(new[] { "can't be blank" }, error.Errors["title"]);
    }

    [Fact]
    public void EnforcesLengthBounds()
    {
        var tooShort = Assert.Throws<ValidationException>(() => TextRules.Parse("username", "ab", 30, 3));
        var tooLong = Assert.Throws<ValidationException>(() => TextRules.Parse("title", new string('a', 11), 10));

        Assert.Equal(new[] { "is too short (minimum is 3 characters)" }, tooShort.Errors["username"]);
        Assert.Equal(new[] { "is too long (maximum is 10 characters)" }, tooLong.Errors["title"]);
    }

    [Fact]
    public void EnforcesThePatternAfterTheLengthChecks()
    {
        var pattern = new Regex("^[a-z]+$");

        var error = Assert.Throws<ValidationException>(() => TextRules.Parse("username", "abc1", 30, 3, pattern));

        Assert.Equal(new[] { "is invalid" }, error.Errors["username"]);
    }
}
