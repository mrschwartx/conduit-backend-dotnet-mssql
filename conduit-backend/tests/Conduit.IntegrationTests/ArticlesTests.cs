using System.Text.Json;
using Conduit.IntegrationTests.Support;

namespace Conduit.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ArticlesTests(ApiFixture api)
{
    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid().ToString("N")[..10]}";

    private async Task<string> CreateAsync(RegisteredUser author, string title, params string[] tags)
    {
        var response = await author.Client.PostAsync("/api/v1/articles", ApiFixture.ArticleJson(title: title, tags: tags));
        Assert.Equal(201, response.Status);
        return response.Property("article").GetProperty("slug").GetString()!;
    }

    [Fact]
    public async Task CreatesAnArticleWithTheFullShape()
    {
        var author = await api.RegisterAsync("auth");
        var title = Unique("Hello World");

        var response = await author.Client.PostAsync("/api/v1/articles", ApiFixture.ArticleJson(title: title, tags: ["dragons", "training"]));

        Assert.Equal(201, response.Status);
        var article = response.Property("article");
        Assert.StartsWith("hello-world-", article.GetProperty("slug").GetString());
        Assert.Equal(title, article.GetProperty("title").GetString());
        Assert.Equal("You have to believe", article.GetProperty("body").GetString());
        Assert.Equal(new[] { "dragons", "training" }, article.GetProperty("tagList").EnumerateArray().Select(tag => tag.GetString()).ToArray());
        Assert.Equal(article.GetProperty("createdAt").GetString(), article.GetProperty("updatedAt").GetString());
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$", article.GetProperty("createdAt").GetString()!);
        Assert.False(article.GetProperty("favorited").GetBoolean());
        Assert.Equal(0, article.GetProperty("favoritesCount").GetInt32());
        Assert.Equal(author.Username, article.GetProperty("author").GetProperty("username").GetString());
        Assert.False(article.GetProperty("author").GetProperty("following").GetBoolean());
    }

    [Fact]
    public async Task GeneratesUniqueSlugsForRepeatedTitles()
    {
        var author = await api.RegisterAsync("slug");
        var title = Unique("Same Title");

        var first = await CreateAsync(author, title);
        var second = await CreateAsync(author, title);

        Assert.NotEqual(first, second);
        Assert.EndsWith("-2", second);
    }

    [Fact]
    public async Task RequiresAuthenticationAndValidatesInput()
    {
        var author = await api.RegisterAsync("val");

        var anonymous = await api.CreateClient().PostAsync("/api/v1/articles", ApiFixture.ArticleJson());
        var invalid = await author.Client.PostAsync(
            "/api/v1/articles",
            ApiClient.Envelope("article", ("title", ""), ("tagList", new[] { "two words", "ok" })));

        Assert.Equal(401, anonymous.Status);
        Assert.Equal(422, invalid.Status);
        Assert.Equal(new[] { "title", "description", "body", "tagList" }, invalid.ErrorFields().ToArray());
        Assert.Equal("cannot contain whitespace or commas inside a tag", invalid.ErrorMessage("tagList"));
    }

    [Fact]
    public async Task ShowsAnArticleToAnonymousAndAuthenticatedReaders()
    {
        var author = await api.RegisterAsync("read");
        var reader = await api.RegisterAsync("rdr");
        var slug = await CreateAsync(author, Unique("Readable"));
        await reader.Client.PostEmptyAsync($"/api/v1/profiles/{author.Username}/follow");
        await reader.Client.PostEmptyAsync($"/api/v1/articles/{slug}/favorite");

        var anonymous = await api.CreateClient().GetAsync($"/api/v1/articles/{slug}");
        var asReader = await reader.Client.GetAsync($"/api/v1/articles/{slug}");

        Assert.False(anonymous.Property("article").GetProperty("favorited").GetBoolean());
        Assert.Equal(1, anonymous.Property("article").GetProperty("favoritesCount").GetInt32());
        Assert.True(asReader.Property("article").GetProperty("favorited").GetBoolean());
        Assert.True(asReader.Property("article").GetProperty("author").GetProperty("following").GetBoolean());
    }

    [Fact]
    public async Task ReturnsNotFoundForUnknownSlugs()
    {
        var response = await api.CreateClient().GetAsync("/api/v1/articles/does-not-exist");

        Assert.Equal(404, response.Status);
        Assert.Equal("not found", response.ErrorMessage("article"));
    }

    [Fact]
    public async Task ListsArticlesWithoutBodiesNewestFirstAndCountsTheTotal()
    {
        var author = await api.RegisterAsync("list");
        var tag = "t" + Guid.NewGuid().ToString("N")[..8];
        var first = await CreateAsync(author, Unique("First"), tag);
        var second = await CreateAsync(author, Unique("Second"), tag);

        var response = await api.CreateClient().GetAsync($"/api/v1/articles?tag={tag}");

        Assert.Equal(200, response.Status);
        Assert.Equal(2, response.Property("articlesCount").GetInt32());
        var articles = response.Property("articles");
        Assert.Equal(second, articles[0].GetProperty("slug").GetString());
        Assert.Equal(first, articles[1].GetProperty("slug").GetString());
        Assert.False(articles[0].TryGetProperty("body", out _));
    }

    [Fact]
    public async Task PagesWithOffsetAndLimit()
    {
        var author = await api.RegisterAsync("page");
        for (var index = 0; index < 3; index++)
        {
            await CreateAsync(author, Unique("Paged"));
        }

        var response = await api.CreateClient().GetAsync($"/api/v1/articles?author={author.Username}&limit=2&offset=1");

        Assert.Equal(3, response.Property("articlesCount").GetInt32());
        Assert.Equal(2, response.Property("articles").GetArrayLength());
    }

    [Fact]
    public async Task FiltersByAuthorAndFavorited()
    {
        var author = await api.RegisterAsync("fa");
        var fan = await api.RegisterAsync("fan");
        var slug = await CreateAsync(author, Unique("Liked"));
        await CreateAsync(author, Unique("Ignored"));
        await fan.Client.PostEmptyAsync($"/api/v1/articles/{slug}/favorite");

        var byAuthor = await api.CreateClient().GetAsync($"/api/v1/articles?author={author.Username.ToUpperInvariant()}");
        var byFavorite = await api.CreateClient().GetAsync($"/api/v1/articles?favorited={fan.Username}");
        var unknown = await api.CreateClient().GetAsync("/api/v1/articles?author=nobody_here_123");

        Assert.Equal(2, byAuthor.Property("articlesCount").GetInt32());
        Assert.Equal(slug, byFavorite.Property("articles")[0].GetProperty("slug").GetString());
        Assert.Equal(0, unknown.Property("articlesCount").GetInt32());
        Assert.Equal(0, unknown.Property("articles").GetArrayLength());
    }

    [Fact]
    public async Task RejectsInvalidPaginationAndRepeatedFilters()
    {
        var invalid = await api.CreateClient().GetAsync("/api/v1/articles?limit=abc&offset=-1");
        var zero = await api.CreateClient().GetAsync("/api/v1/articles?limit=0");
        var repeated = await api.CreateClient().GetAsync("/api/v1/articles?tag=a&tag=b");

        Assert.Equal(422, invalid.Status);
        Assert.Equal("must be an integer", invalid.ErrorMessage("limit"));
        Assert.Equal("must be an integer", invalid.ErrorMessage("offset"));
        Assert.Equal("must be at least 1", zero.ErrorMessage("limit"));
        Assert.Equal(422, repeated.Status);
        Assert.Equal("is invalid", repeated.ErrorMessage("tag"));
    }

    [Fact]
    public async Task BuildsTheFeedFromFollowedAuthors()
    {
        var author = await api.RegisterAsync("feeda");
        var reader = await api.RegisterAsync("feedr");
        var slug = await CreateAsync(author, Unique("In Feed"));
        await CreateAsync(reader, Unique("Own Article"));

        var before = await reader.Client.GetAsync("/api/v1/articles/feed");
        await reader.Client.PostEmptyAsync($"/api/v1/profiles/{author.Username}/follow");
        var after = await reader.Client.GetAsync("/api/v1/articles/feed");
        var anonymous = await api.CreateClient().GetAsync("/api/v1/articles/feed");

        Assert.Equal(0, before.Property("articlesCount").GetInt32());
        Assert.Equal(1, after.Property("articlesCount").GetInt32());
        Assert.Equal(slug, after.Property("articles")[0].GetProperty("slug").GetString());
        Assert.True(after.Property("articles")[0].GetProperty("author").GetProperty("following").GetBoolean());
        Assert.Equal(401, anonymous.Status);
    }

    [Fact]
    public async Task UpdatesAnArticleAndRegeneratesTheSlugOnTitleChange()
    {
        var author = await api.RegisterAsync("upd");
        var slug = await CreateAsync(author, Unique("Original"), "old");
        var newTitle = Unique("Renamed");

        var response = await author.Client.PutAsync(
            $"/api/v1/articles/{slug}",
            ApiClient.Envelope("article", ("title", newTitle), ("tagList", new[] { "fresh" })));

        Assert.Equal(200, response.Status);
        var article = response.Property("article");
        Assert.StartsWith("renamed-", article.GetProperty("slug").GetString());
        Assert.Equal("fresh", article.GetProperty("tagList")[0].GetString());
        Assert.Equal("Ever wonder how?", article.GetProperty("description").GetString());
        Assert.Equal(404, (await api.CreateClient().GetAsync($"/api/v1/articles/{slug}")).Status);
    }

    [Fact]
    public async Task KeepsTheSlugWhenTheTitleIsUnchanged()
    {
        var author = await api.RegisterAsync("keep");
        var title = Unique("Stable");
        var slug = await CreateAsync(author, title);

        var response = await author.Client.PutAsync(
            $"/api/v1/articles/{slug}",
            ApiClient.Envelope("article", ("title", title), ("body", "Edited body")));

        Assert.Equal(slug, response.Property("article").GetProperty("slug").GetString());
        Assert.Equal("Edited body", response.Property("article").GetProperty("body").GetString());
    }

    [Fact]
    public async Task ChecksAuthenticationThenExistenceThenOwnershipThenValidationOnUpdate()
    {
        var author = await api.RegisterAsync("own");
        var stranger = await api.RegisterAsync("str");
        var slug = await CreateAsync(author, Unique("Owned"));
        var invalidBody = ApiClient.Envelope("article", ("title", ""));

        var anonymous = await api.CreateClient().PutAsync($"/api/v1/articles/{slug}", invalidBody);
        var missing = await stranger.Client.PutAsync("/api/v1/articles/nope-nope", invalidBody);
        var foreign = await stranger.Client.PutAsync($"/api/v1/articles/{slug}", invalidBody);
        var invalid = await author.Client.PutAsync($"/api/v1/articles/{slug}", invalidBody);
        var empty = await author.Client.PutAsync($"/api/v1/articles/{slug}", ApiClient.Envelope("article"));

        Assert.Equal(401, anonymous.Status);
        Assert.Equal(404, missing.Status);
        Assert.Equal(403, foreign.Status);
        Assert.Equal("forbidden", foreign.ErrorMessage("article"));
        Assert.Equal(422, invalid.Status);
        Assert.Equal(422, empty.Status);
        Assert.Equal("can't be empty", empty.ErrorMessage("article"));
    }

    [Fact]
    public async Task DeletesOnlyOwnArticles()
    {
        var author = await api.RegisterAsync("del");
        var stranger = await api.RegisterAsync("nodel");
        var slug = await CreateAsync(author, Unique("Doomed"));

        var anonymous = await api.CreateClient().DeleteAsync($"/api/v1/articles/{slug}");
        var foreign = await stranger.Client.DeleteAsync($"/api/v1/articles/{slug}");
        var deleted = await author.Client.DeleteAsync($"/api/v1/articles/{slug}");
        var again = await author.Client.DeleteAsync($"/api/v1/articles/{slug}");

        Assert.Equal(401, anonymous.Status);
        Assert.Equal(403, foreign.Status);
        Assert.Equal(204, deleted.Status);
        Assert.Equal(string.Empty, deleted.Body);
        Assert.Equal(404, again.Status);
    }

    [Fact]
    public async Task FavoritesAndUnfavoritesIdempotently()
    {
        var author = await api.RegisterAsync("favauth");
        var fan = await api.RegisterAsync("favfan");
        var slug = await CreateAsync(author, Unique("Favorite"));

        var first = await fan.Client.PostEmptyAsync($"/api/v1/articles/{slug}/favorite");
        var second = await fan.Client.PostEmptyAsync($"/api/v1/articles/{slug}/favorite");
        var removed = await fan.Client.DeleteAsync($"/api/v1/articles/{slug}/favorite");
        var again = await fan.Client.DeleteAsync($"/api/v1/articles/{slug}/favorite");

        Assert.True(first.Property("article").GetProperty("favorited").GetBoolean());
        Assert.Equal(1, second.Property("article").GetProperty("favoritesCount").GetInt32());
        Assert.False(removed.Property("article").GetProperty("favorited").GetBoolean());
        Assert.Equal(0, again.Property("article").GetProperty("favoritesCount").GetInt32());
        Assert.Equal(401, (await api.CreateClient().PostEmptyAsync($"/api/v1/articles/{slug}/favorite")).Status);
        Assert.Equal(404, (await fan.Client.PostEmptyAsync("/api/v1/articles/nope-nope/favorite")).Status);
    }

    [Fact]
    public async Task ListsPopularTags()
    {
        var author = await api.RegisterAsync("tags");
        var popular = "pop" + Guid.NewGuid().ToString("N")[..8];
        await CreateAsync(author, Unique("One"), popular);
        await CreateAsync(author, Unique("Two"), popular);

        var response = await api.CreateClient().GetAsync("/api/v1/tags");

        Assert.Equal(200, response.Status);
        var tags = response.Property("tags").EnumerateArray().Select(tag => tag.GetString()).ToList();
        Assert.Contains(popular, tags);
        Assert.True(tags.Count <= 20);
    }

    [Fact]
    public async Task DoesNotTreatFeedAsAnArticleSlug()
    {
        var author = await api.RegisterAsync("feedslug");

        var response = await author.Client.PostAsync("/api/v1/articles", ApiFixture.ArticleJson(title: "Feed"));

        Assert.Equal(201, response.Status);
        Assert.NotEqual("feed", response.Property("article").GetProperty("slug").GetString());
        var feed = await author.Client.GetAsync("/api/v1/articles/feed");
        Assert.Equal(200, feed.Status);
        Assert.Equal(JsonValueKind.Array, feed.Property("articles").ValueKind);
    }
}
