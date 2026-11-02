using Conduit.Modules.Articles.Application;
using Conduit.SharedKernel.Domain;
using Conduit.UnitTests.Support;

namespace Conduit.UnitTests.Articles;

public sealed class ArticleUseCaseTests
{
    private readonly InMemoryArticleRepository articles = new();

    private readonly FakeAuthorDirectory authors = new FakeAuthorDirectory()
        .With(Ids.User, "john")
        .With(Ids.OtherUser, "jane");

    private readonly FixedTimeProvider clock = new(Ids.FixedNow);

    private readonly RecordingEventBus events = new();

    private ArticleSlugs Slugs => new(articles);

    private CreateArticle Create => new(articles, Slugs, authors, clock);

    private UpdateArticle Update => new(articles, Slugs, authors, clock);

    private static IReadOnlyDictionary<string, object?> NewArticle(string title = "Hello World", params string[] tags) =>
        Input.Of(("title", title), ("description", "About greetings"), ("body", "Body text"), ("tagList", Input.Tags(tags)));

    [Fact]
    public async Task CreatesAnArticleWithSlugAndAuthor()
    {
        var result = await Create.ExecuteAsync(Ids.User, NewArticle("Hello World", "a", "b"));

        Assert.Equal("hello-world", result.Slug);
        Assert.Equal("Body text", result.Body);
        Assert.Equal(new[] { "a", "b" }, result.TagList.ToArray());
        Assert.Equal("2026-01-01T00:00:00.000Z", result.CreatedAt);
        Assert.Equal(result.CreatedAt, result.UpdatedAt);
        Assert.False(result.Favorited);
        Assert.Equal(0, result.FavoritesCount);
        Assert.Equal("john", result.Author.Username);
        Assert.False(result.Author.Following);
    }

    [Fact]
    public async Task CreatesAnArticleWithoutTags()
    {
        var result = await Create.ExecuteAsync(Ids.User, Input.Of(("title", "T"), ("description", "D"), ("body", "B")));

        Assert.Empty(result.TagList);
    }

    [Fact]
    public async Task ReportsEveryInvalidArticleField()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(
            () => Create.ExecuteAsync(Ids.User, Input.Of(("title", ""), ("tagList", Input.Tags("two words")))));

        Assert.Equal(new[] { "title", "description", "body", "tagList" }, error.Errors.Keys.ToArray());
        Assert.Empty(articles.Items);
    }

    [Fact]
    public async Task AssignsUniqueSlugsToRepeatedTitles()
    {
        var first = await Create.ExecuteAsync(Ids.User, NewArticle());
        var second = await Create.ExecuteAsync(Ids.OtherUser, NewArticle());
        var third = await Create.ExecuteAsync(Ids.User, NewArticle());

        Assert.Equal("hello-world", first.Slug);
        Assert.Equal("hello-world-2", second.Slug);
        Assert.Equal("hello-world-3", third.Slug);
    }

    [Fact]
    public async Task NeverUsesTheReservedFeedSlug()
    {
        var result = await Create.ExecuteAsync(Ids.User, NewArticle("Feed"));

        Assert.Equal("feed-2", result.Slug);
    }

    [Fact]
    public async Task RetriesWhenAConcurrentRequestTakesTheSlug()
    {
        articles.SlugCollisionsToSimulate = 2;

        var result = await Create.ExecuteAsync(Ids.User, NewArticle());

        Assert.Equal("hello-world", result.Slug);
        Assert.Single(articles.Items);
    }

    [Fact]
    public async Task GivesUpAfterTheMaximumNumberOfSlugCollisions()
    {
        articles.SlugCollisionsToSimulate = ArticleSlugs.MaxAttempts;

        await Assert.ThrowsAsync<Conduit.Modules.Articles.Domain.SlugTakenException>(
            () => Create.ExecuteAsync(Ids.User, NewArticle()));
    }

    [Fact]
    public async Task GetsAnArticleWithViewerState()
    {
        var article = articles.Seed(Ids.User, "Hello World");
        articles.Favorites.Add((Ids.OtherUser, article.Id));
        authors.Follows.Add((Ids.OtherUser, Ids.User));
        var get = new GetArticle(articles, authors);

        var asViewer = await get.ExecuteAsync("hello-world", Ids.OtherUser);
        var asAnonymous = await get.ExecuteAsync("hello-world", null);

        Assert.True(asViewer.Favorited);
        Assert.Equal(1, asViewer.FavoritesCount);
        Assert.True(asViewer.Author.Following);
        Assert.False(asAnonymous.Favorited);
        Assert.False(asAnonymous.Author.Following);
    }

    [Fact]
    public async Task ReportsMissingArticles()
    {
        var error = await Assert.ThrowsAsync<NotFoundException>(() => new GetArticle(articles, authors).ExecuteAsync("nope", null));

        Assert.Equal("article", error.Resource);
    }

    [Fact]
    public async Task ListsNewestFirstWithoutBodiesAndCountsTheTotal()
    {
        articles.Seed(Ids.User, "First");
        articles.Seed(Ids.OtherUser, "Second");
        articles.Seed(Ids.User, "Third");
        var list = new ListArticles(articles, authors);

        var page = await list.ExecuteAsync(null, Input.Of(("limit", "2")));

        Assert.Equal(3, page.ArticlesCount);
        Assert.Equal(new[] { "third", "second" }, page.Articles.Select(item => item.Slug).ToArray());
    }

    [Fact]
    public async Task PagesThroughResults()
    {
        for (var index = 1; index <= 5; index++)
        {
            articles.Seed(Ids.User, $"Article {index}");
        }

        var page = await new ListArticles(articles, authors).ExecuteAsync(null, Input.Of(("offset", "3"), ("limit", "10")));

        Assert.Equal(5, page.ArticlesCount);
        Assert.Equal(new[] { "article-2", "article-1" }, page.Articles.Select(item => item.Slug).ToArray());
    }

    [Fact]
    public async Task FiltersByTagAuthorAndFavorite()
    {
        var first = articles.Seed(Ids.User, "First", "dotnet");
        articles.Seed(Ids.OtherUser, "Second", "dotnet", "web");
        articles.Favorites.Add((Ids.OtherUser, first.Id));
        var list = new ListArticles(articles, authors);

        var byTag = await list.ExecuteAsync(null, Input.Of(("tag", "web")));
        var byAuthor = await list.ExecuteAsync(null, Input.Of(("author", "john")));
        var byFavorite = await list.ExecuteAsync(null, Input.Of(("favorited", "jane")));
        var combined = await list.ExecuteAsync(null, Input.Of(("tag", "dotnet"), ("author", "jane")));

        Assert.Equal(new[] { "second" }, byTag.Articles.Select(item => item.Slug).ToArray());
        Assert.Equal(new[] { "first" }, byAuthor.Articles.Select(item => item.Slug).ToArray());
        Assert.Equal(new[] { "first" }, byFavorite.Articles.Select(item => item.Slug).ToArray());
        Assert.Equal(new[] { "second" }, combined.Articles.Select(item => item.Slug).ToArray());
    }

    [Fact]
    public async Task ReturnsAnEmptyPageForUnknownAuthorsAndFavoriters()
    {
        articles.Seed(Ids.User, "First");
        var list = new ListArticles(articles, authors);

        var unknownAuthor = await list.ExecuteAsync(null, Input.Of(("author", "ghost")));
        var unknownFavoriter = await list.ExecuteAsync(null, Input.Of(("favorited", "ghost")));

        Assert.Empty(unknownAuthor.Articles);
        Assert.Equal(0, unknownAuthor.ArticlesCount);
        Assert.Empty(unknownFavoriter.Articles);
    }

    [Fact]
    public async Task ValidatesPaginationAndFiltersTogether()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(
            () => new ListArticles(articles, authors).ExecuteAsync(
                null,
                Input.Of(("limit", "abc"), ("offset", "-1"), ("tag", new[] { "a", "b" }), ("author", new[] { "a", "b" }))));

        Assert.Equal(new[] { "offset", "limit", "tag", "author" }, error.Errors.Keys.ToArray());
        Assert.Equal(new[] { "is invalid" }, error.Errors["tag"]);
    }

    [Fact]
    public async Task BuildsTheFeedFromFollowedAuthors()
    {
        articles.Seed(Ids.User, "Mine");
        articles.Seed(Ids.OtherUser, "Theirs");
        authors.Follows.Add((Ids.User, Ids.OtherUser));
        var feed = new GetFeed(articles, authors);

        var page = await feed.ExecuteAsync(Ids.User, Input.Empty);

        Assert.Equal(new[] { "theirs" }, page.Articles.Select(item => item.Slug).ToArray());
        Assert.True(page.Articles[0].Author.Following);
    }

    [Fact]
    public async Task ReturnsAnEmptyFeedWhenFollowingNobody()
    {
        articles.Seed(Ids.OtherUser, "Theirs");

        var page = await new GetFeed(articles, authors).ExecuteAsync(Ids.User, Input.Empty);

        Assert.Empty(page.Articles);
        Assert.Equal(0, page.ArticlesCount);
    }

    [Fact]
    public async Task ValidatesFeedPagination()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(
            () => new GetFeed(articles, authors).ExecuteAsync(Ids.User, Input.Of(("limit", "0"))));

        Assert.Equal(new[] { "must be at least 1" }, error.Errors["limit"]);
    }

    [Fact]
    public async Task UpdatesFieldsAndKeepsTheSlugWhenTheTitleIsUnchanged()
    {
        articles.Seed(Ids.User, "Hello World", "a");

        var result = await Update.ExecuteAsync(
            Ids.User,
            "hello-world",
            Input.Of(("title", "Hello World"), ("body", "New body"), ("tagList", Input.Tags("b"))));

        Assert.Equal("hello-world", result.Slug);
        Assert.Equal("New body", result.Body);
        Assert.Equal(new[] { "b" }, result.TagList.ToArray());
    }

    [Fact]
    public async Task ChangesTheSlugWhenTheTitleChanges()
    {
        articles.Seed(Ids.User, "Hello World");

        var result = await Update.ExecuteAsync(Ids.User, "hello-world", Input.Of(("title", "Brand New Title")));

        Assert.Equal("brand-new-title", result.Slug);
        Assert.Null(await articles.FindBySlugAsync("hello-world"));
    }

    [Fact]
    public async Task AvoidsSlugsOwnedByOtherArticlesButNotItsOwn()
    {
        articles.Seed(Ids.User, "Taken");
        articles.Seed(Ids.User, "Mine");

        var renamed = await Update.ExecuteAsync(Ids.User, "mine", Input.Of(("title", "Taken")));

        Assert.Equal("taken-2", renamed.Slug);
    }

    [Fact]
    public async Task ClearsTagsWithNull()
    {
        articles.Seed(Ids.User, "Hello World", "a");

        var result = await Update.ExecuteAsync(Ids.User, "hello-world", Input.Of(("tagList", null)));

        Assert.Empty(result.TagList);
    }

    [Fact]
    public async Task ChecksExistenceThenOwnershipThenValidationOnUpdate()
    {
        articles.Seed(Ids.OtherUser, "Hello World");

        var missing = await Assert.ThrowsAsync<NotFoundException>(
            () => Update.ExecuteAsync(Ids.User, "nope", Input.Of(("title", ""))));
        var foreign = await Assert.ThrowsAsync<ForbiddenException>(
            () => Update.ExecuteAsync(Ids.User, "hello-world", Input.Of(("title", ""))));
        articles.Seed(Ids.User, "Mine");
        var invalid = await Assert.ThrowsAsync<ValidationException>(
            () => Update.ExecuteAsync(Ids.User, "mine", Input.Of(("title", ""))));

        Assert.Equal("article", missing.Resource);
        Assert.Equal("article", foreign.Resource);
        Assert.Equal(new[] { "can't be blank" }, invalid.Errors["title"]);
    }

    [Fact]
    public async Task RejectsEmptyUpdates()
    {
        articles.Seed(Ids.User, "Hello World");

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => Update.ExecuteAsync(Ids.User, "hello-world", Input.Of(("unknown", "x"))));

        Assert.Equal(new[] { "can't be empty" }, error.Errors["article"]);
    }

    [Fact]
    public async Task DeletesOwnArticlesAndPublishesAnEvent()
    {
        var article = articles.Seed(Ids.User, "Hello World");
        var delete = new DeleteArticle(articles, events, clock);

        await delete.ExecuteAsync(Ids.User, "hello-world");

        Assert.Empty(articles.Items);
        var published = Assert.IsType<ArticleDeleted>(Assert.Single(events.Published));
        Assert.Equal(article.Id, published.ArticleId);
    }

    [Fact]
    public async Task RefusesToDeleteForeignOrMissingArticles()
    {
        articles.Seed(Ids.OtherUser, "Hello World");
        var delete = new DeleteArticle(articles, events, clock);

        await Assert.ThrowsAsync<ForbiddenException>(() => delete.ExecuteAsync(Ids.User, "hello-world"));
        await Assert.ThrowsAsync<NotFoundException>(() => delete.ExecuteAsync(Ids.User, "nope"));

        Assert.Single(articles.Items);
        Assert.Empty(events.Published);
    }

    [Fact]
    public async Task FavoritesAndUnfavoritesIdempotently()
    {
        articles.Seed(Ids.OtherUser, "Hello World");
        var favorite = new SetFavorite(articles, authors);

        var first = await favorite.ExecuteAsync(Ids.User, "hello-world", true);
        var second = await favorite.ExecuteAsync(Ids.User, "hello-world", true);
        var removed = await favorite.ExecuteAsync(Ids.User, "hello-world", false);
        var again = await favorite.ExecuteAsync(Ids.User, "hello-world", false);

        Assert.True(first.Favorited);
        Assert.Equal(1, second.FavoritesCount);
        Assert.False(removed.Favorited);
        Assert.Equal(0, again.FavoritesCount);
    }

    [Fact]
    public async Task ReportsMissingArticlesWhenFavoriting()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => new SetFavorite(articles, authors).ExecuteAsync(Ids.User, "nope", true));
    }

    [Fact]
    public async Task ListsPopularTagsByFrequencyThenName()
    {
        articles.Seed(Ids.User, "One", "b", "a");
        articles.Seed(Ids.User, "Two", "b", "c");
        articles.Seed(Ids.User, "Three", "b");

        var tags = await new ListPopularTags(articles).ExecuteAsync();

        Assert.Equal(new[] { "b", "a", "c" }, tags.ToArray());
    }

    [Fact]
    public async Task ResolvesArticleIdsThroughTheCatalog()
    {
        var article = articles.Seed(Ids.User, "Hello World");
        var catalog = new ArticleCatalog(articles);

        Assert.Equal(article.Id, await catalog.FindIdBySlugAsync("hello-world"));
        Assert.Null(await catalog.FindIdBySlugAsync("nope"));
    }
}
