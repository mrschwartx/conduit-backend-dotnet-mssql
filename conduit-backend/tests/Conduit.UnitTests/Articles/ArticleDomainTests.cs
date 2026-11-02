using Conduit.Modules.Articles.Domain;
using Conduit.SharedKernel.Domain;
using Conduit.UnitTests.Support;

namespace Conduit.UnitTests.Articles;

public sealed class ArticleDomainTests
{
    [Theory]
    [InlineData("Hello World", "hello-world")]
    [InlineData("  Hello,   World!! ", "hello-world")]
    [InlineData("Café au lait", "cafe-au-lait")]
    [InlineData("Ünïcödé Straße", "unicode-stra-e")]
    [InlineData("ﬁne ½ day", "fine-1-2-day")]
    [InlineData("C# & .NET 10", "c-net-10")]
    [InlineData("---", "article")]
    [InlineData("日本語", "article")]
    public void BuildsSlugsFromTitles(string title, string expected)
    {
        Assert.Equal(expected, Slug.From(title));
    }

    [Fact]
    public void CapsSlugsAtEightyCharactersWithoutTrailingDashes()
    {
        var slug = Slug.From(string.Join(" ", Enumerable.Repeat("word", 40)));

        Assert.True(slug.Length <= 80);
        Assert.False(slug.EndsWith('-'));
        Assert.StartsWith("word-word", slug);
    }

    [Fact]
    public void AppendsNumericSuffixesToCandidates()
    {
        Assert.Equal("hello", Slug.Candidate("hello", 1));
        Assert.Equal("hello-2", Slug.Candidate("hello", 2));
        Assert.Equal("hello-12", Slug.Candidate("hello", 12));
    }

    [Fact]
    public void KeepsSuffixedCandidatesWithinTheLimit()
    {
        var baseSlug = new string('a', 80);

        var candidate = Slug.Candidate(baseSlug, 10);

        Assert.Equal(80, candidate.Length);
        Assert.EndsWith("-10", candidate);
    }

    [Fact]
    public void ReservesTheFeedSegment()
    {
        Assert.True(Slug.IsReserved("feed"));
        Assert.False(Slug.IsReserved("feeds"));
        Assert.Equal("feed", Slug.From("Feed"));
    }

    [Fact]
    public void ValidatesArticleTexts()
    {
        Assert.Equal("Title", ArticleTitle.Create("  Title ").Value);

        var blank = Assert.Throws<ValidationException>(() => ArticleTitle.Create("  "));
        var longTitle = Assert.Throws<ValidationException>(() => ArticleTitle.Create(new string('a', 101)));
        var longDescription = Assert.Throws<ValidationException>(() => ArticleDescription.Create(new string('a', 201)));
        var longBody = Assert.Throws<ValidationException>(() => ArticleBody.Create(new string('a', 20001)));
        var missingBody = Assert.Throws<ValidationException>(() => ArticleBody.Create(null));

        Assert.Equal(new[] { "can't be blank" }, blank.Errors["title"]);
        Assert.Equal(new[] { "is too long (maximum is 100 characters)" }, longTitle.Errors["title"]);
        Assert.Equal(new[] { "is too long (maximum is 200 characters)" }, longDescription.Errors["description"]);
        Assert.Equal(new[] { "is too long (maximum is 20000 characters)" }, longBody.Errors["body"]);
        Assert.Equal(new[] { "can't be blank" }, missingBody.Errors["body"]);
    }

    [Fact]
    public void TreatsAMissingTagListAsEmpty()
    {
        Assert.Empty(TagList.Create(null).Values);
        Assert.Empty(TagList.Create(Input.Tags()).Values);
    }

    [Fact]
    public void TrimsAndDeduplicatesTagsKeepingTheFirstOccurrence()
    {
        var tags = TagList.Create(Input.Tags("b", " a ", "b", "c", "a"));

        Assert.Equal(new[] { "b", "a", "c" }, tags.Values.ToArray());
    }

    [Fact]
    public void ComparesTagListsByOrder()
    {
        Assert.True(TagList.Create(Input.Tags("a", "b")).SameAs(TagList.Create(Input.Tags("a", "b"))));
        Assert.False(TagList.Create(Input.Tags("a", "b")).SameAs(TagList.Create(Input.Tags("b", "a"))));
    }

    [Theory]
    [InlineData("not-a-list", "is invalid")]
    public void RejectsNonListTagLists(object raw, string message)
    {
        var error = Assert.Throws<ValidationException>(() => TagList.Create(raw));

        Assert.Equal(new[] { message }, error.Errors["tagList"]);
    }

    [Fact]
    public void RejectsInvalidTagEntries()
    {
        var notText = Assert.Throws<ValidationException>(() => TagList.Create(new List<object?> { 5 }));
        var blank = Assert.Throws<ValidationException>(() => TagList.Create(Input.Tags("ok", "  ")));
        var inner = Assert.Throws<ValidationException>(() => TagList.Create(Input.Tags("two words")));
        var comma = Assert.Throws<ValidationException>(() => TagList.Create(Input.Tags("a,b")));
        var tooLong = Assert.Throws<ValidationException>(() => TagList.Create(Input.Tags(new string('a', 31))));

        Assert.Equal(new[] { "is invalid" }, notText.Errors["tagList"]);
        Assert.Equal(new[] { "can't contain blank tags" }, blank.Errors["tagList"]);
        Assert.Equal(new[] { "cannot contain whitespace or commas inside a tag" }, inner.Errors["tagList"]);
        Assert.Equal(new[] { "cannot contain whitespace or commas inside a tag" }, comma.Errors["tagList"]);
        Assert.Equal(new[] { "cannot contain a tag longer than 30 characters" }, tooLong.Errors["tagList"]);
    }

    [Fact]
    public void LimitsTheNumberOfDistinctTags()
    {
        var ten = Enumerable.Range(1, 10).Select(index => $"t{index}").ToArray();
        var eleven = Enumerable.Range(1, 11).Select(index => $"t{index}").ToArray();

        Assert.Equal(10, TagList.Create(Input.Tags(ten)).Values.Count);
        Assert.Equal(10, TagList.Create(Input.Tags(ten.Concat(ten).ToArray())).Values.Count);
        var error = Assert.Throws<ValidationException>(() => TagList.Create(Input.Tags(eleven)));
        Assert.Equal(new[] { "is too long (maximum is 10 tags)" }, error.Errors["tagList"]);
    }

    [Fact]
    public void RevisesOnlyTheProvidedParts()
    {
        var article = Article.Create(
            Ids.Article,
            Ids.User,
            "old-title",
            ArticleTitle.Create("Old title"),
            ArticleDescription.Create("Description"),
            ArticleBody.Create("Body"),
            TagList.Create(Input.Tags("a")),
            Ids.FixedNow);
        var later = Ids.FixedNow.AddMinutes(5);

        article.Revise("new-title", ArticleTitle.Create("New title"), null, null, null, later);

        Assert.Equal("new-title", article.Slug);
        Assert.Equal("New title", article.Title.Value);
        Assert.Equal("Description", article.Description.Value);
        Assert.Equal(new[] { "a" }, article.TagList.Values.ToArray());
        Assert.Equal(later, article.UpdatedAt);
        Assert.Equal(Ids.FixedNow, article.CreatedAt);
        Assert.True(article.IsAuthoredBy(Ids.User));
        Assert.False(article.IsAuthoredBy(Ids.OtherUser));
    }
}
