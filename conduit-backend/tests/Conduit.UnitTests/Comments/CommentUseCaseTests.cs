using Conduit.Modules.Comments.Application;
using Conduit.SharedKernel.Domain;
using Conduit.UnitTests.Support;

namespace Conduit.UnitTests.Comments;

public sealed class CommentUseCaseTests
{
    private readonly InMemoryArticleRepository articles = new();

    private readonly InMemoryCommentRepository comments = new();

    private readonly FakeAuthorDirectory authors = new FakeAuthorDirectory()
        .With(Ids.User, "john")
        .With(Ids.OtherUser, "jane");

    private readonly FixedTimeProvider clock = new(Ids.FixedNow);

    private CreateComment Create => new(comments, new FakeArticleCatalog(articles), authors, clock);

    private ListComments List => new(comments, new FakeArticleCatalog(articles), authors);

    private DeleteComment Delete => new(comments, new FakeArticleCatalog(articles));

    [Fact]
    public async Task CreatesACommentWithItsAuthor()
    {
        articles.Seed(Ids.OtherUser, "Hello World");

        var result = await Create.ExecuteAsync(Ids.User, "hello-world", Input.Of(("body", "  Nice post ")));

        Assert.Equal(1, result.Id);
        Assert.Equal("Nice post", result.Body);
        Assert.Equal("2026-01-01T00:00:00.000Z", result.CreatedAt);
        Assert.Equal("john", result.Author.Username);
    }

    [Fact]
    public async Task ChecksTheArticleBeforeValidatingTheBody()
    {
        var missing = await Assert.ThrowsAsync<NotFoundException>(
            () => Create.ExecuteAsync(Ids.User, "nope", Input.Of(("body", ""))));
        articles.Seed(Ids.OtherUser, "Hello World");
        var blank = await Assert.ThrowsAsync<ValidationException>(
            () => Create.ExecuteAsync(Ids.User, "hello-world", Input.Of(("body", " "))));
        var tooLong = await Assert.ThrowsAsync<ValidationException>(
            () => Create.ExecuteAsync(Ids.User, "hello-world", Input.Of(("body", new string('a', 1001)))));

        Assert.Equal("article", missing.Resource);
        Assert.Equal(new[] { "can't be blank" }, blank.Errors["body"]);
        Assert.Equal(new[] { "is too long (maximum is 1000 characters)" }, tooLong.Errors["body"]);
    }

    [Fact]
    public async Task ListsCommentsNewestFirstWithTheViewersFollowingFlag()
    {
        articles.Seed(Ids.OtherUser, "Hello World");
        await Create.ExecuteAsync(Ids.User, "hello-world", Input.Of(("body", "first")));
        await Create.ExecuteAsync(Ids.OtherUser, "hello-world", Input.Of(("body", "second")));
        authors.Follows.Add((Ids.User, Ids.OtherUser));

        var asViewer = await List.ExecuteAsync("hello-world", Ids.User);
        var asAnonymous = await List.ExecuteAsync("hello-world", null);

        Assert.Equal(new[] { "second", "first" }, asViewer.Select(comment => comment.Body).ToArray());
        Assert.True(asViewer[0].Author.Following);
        Assert.False(asViewer[1].Author.Following);
        Assert.All(asAnonymous, comment => Assert.False(comment.Author.Following));
    }

    [Fact]
    public async Task ReturnsNoCommentsForAnArticleWithoutAny()
    {
        articles.Seed(Ids.OtherUser, "Hello World");

        Assert.Empty(await List.ExecuteAsync("hello-world", null));
        await Assert.ThrowsAsync<NotFoundException>(() => List.ExecuteAsync("nope", null));
    }

    [Fact]
    public async Task DeletesOwnComments()
    {
        articles.Seed(Ids.OtherUser, "Hello World");
        var comment = await Create.ExecuteAsync(Ids.User, "hello-world", Input.Of(("body", "bye")));

        await Delete.ExecuteAsync(Ids.User, "hello-world", comment.Id.ToString());

        Assert.Empty(comments.Items);
    }

    [Fact]
    public async Task ValidatesTheDeleteTargetInOrder()
    {
        articles.Seed(Ids.OtherUser, "Hello World");
        articles.Seed(Ids.OtherUser, "Other Article");
        var comment = await Create.ExecuteAsync(Ids.OtherUser, "hello-world", Input.Of(("body", "mine")));

        var noArticle = await Assert.ThrowsAsync<NotFoundException>(() => Delete.ExecuteAsync(Ids.User, "nope", "x"));
        var notInteger = await Assert.ThrowsAsync<ValidationException>(() => Delete.ExecuteAsync(Ids.User, "hello-world", "abc"));
        var tooBig = await Assert.ThrowsAsync<NotFoundException>(() => Delete.ExecuteAsync(Ids.User, "hello-world", new string('9', 19)));
        var unknown = await Assert.ThrowsAsync<NotFoundException>(() => Delete.ExecuteAsync(Ids.User, "hello-world", "999"));
        var wrongArticle = await Assert.ThrowsAsync<NotFoundException>(
            () => Delete.ExecuteAsync(Ids.User, "other-article", comment.Id.ToString()));
        var foreign = await Assert.ThrowsAsync<ForbiddenException>(
            () => Delete.ExecuteAsync(Ids.User, "hello-world", comment.Id.ToString()));

        Assert.Equal("article", noArticle.Resource);
        Assert.Equal(new[] { "must be an integer" }, notInteger.Errors["id"]);
        Assert.Equal("comment", tooBig.Resource);
        Assert.Equal("comment", unknown.Resource);
        Assert.Equal("comment", wrongArticle.Resource);
        Assert.Equal("comment", foreign.Resource);
        Assert.Single(comments.Items);
    }

    [Fact]
    public async Task RemovesCommentsWhenTheirArticleIsDeleted()
    {
        var article = articles.Seed(Ids.OtherUser, "Hello World");
        var other = articles.Seed(Ids.OtherUser, "Other Article");
        await Create.ExecuteAsync(Ids.User, "hello-world", Input.Of(("body", "one")));
        await Create.ExecuteAsync(Ids.User, "other-article", Input.Of(("body", "two")));
        var handler = new ArticleDeletedHandler(new DeleteCommentsByArticle(comments));

        await handler.HandleAsync(new ArticleDeleted(article.Id, Ids.FixedNow), CancellationToken.None);

        Assert.Equal(other.Id, Assert.Single(comments.Items).ArticleId);
    }
}
