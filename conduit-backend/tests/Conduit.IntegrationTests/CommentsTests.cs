using Conduit.IntegrationTests.Support;

namespace Conduit.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class CommentsTests(ApiFixture api)
{
    private async Task<string> CreateArticleAsync(RegisteredUser author)
    {
        var response = await author.Client.PostAsync(
            "/api/v1/articles",
            ApiFixture.ArticleJson(title: "Commented " + Guid.NewGuid().ToString("N")[..10]));
        return response.Property("article").GetProperty("slug").GetString()!;
    }

    private static string CommentJson(string body) => ApiClient.Envelope("comment", ("body", body));

    [Fact]
    public async Task CreatesACommentWithAnIntegerId()
    {
        var author = await api.RegisterAsync("cauth");
        var commenter = await api.RegisterAsync("cmt");
        var slug = await CreateArticleAsync(author);

        var response = await commenter.Client.PostAsync($"/api/v1/articles/{slug}/comments", CommentJson("Thank you so much!"));

        Assert.Equal(201, response.Status);
        var comment = response.Property("comment");
        Assert.True(comment.GetProperty("id").GetInt64() > 0);
        Assert.Equal("Thank you so much!", comment.GetProperty("body").GetString());
        Assert.Equal(comment.GetProperty("createdAt").GetString(), comment.GetProperty("updatedAt").GetString());
        Assert.Equal(commenter.Username, comment.GetProperty("author").GetProperty("username").GetString());
    }

    [Fact]
    public async Task ListsCommentsNewestFirstForAnonymousReaders()
    {
        var author = await api.RegisterAsync("lauth");
        var slug = await CreateArticleAsync(author);
        await author.Client.PostAsync($"/api/v1/articles/{slug}/comments", CommentJson("first"));
        await author.Client.PostAsync($"/api/v1/articles/{slug}/comments", CommentJson("second"));

        var response = await api.CreateClient().GetAsync($"/api/v1/articles/{slug}/comments");

        Assert.Equal(200, response.Status);
        var comments = response.Property("comments");
        Assert.Equal(2, comments.GetArrayLength());
        Assert.Equal("second", comments[0].GetProperty("body").GetString());
        Assert.Equal("first", comments[1].GetProperty("body").GetString());
    }

    [Fact]
    public async Task ShowsTheFollowingFlagToAuthenticatedReaders()
    {
        var author = await api.RegisterAsync("fauth");
        var reader = await api.RegisterAsync("frd");
        var slug = await CreateArticleAsync(author);
        await author.Client.PostAsync($"/api/v1/articles/{slug}/comments", CommentJson("hello"));
        await reader.Client.PostEmptyAsync($"/api/v1/profiles/{author.Username}/follow");

        var response = await reader.Client.GetAsync($"/api/v1/articles/{slug}/comments");

        Assert.True(response.Property("comments")[0].GetProperty("author").GetProperty("following").GetBoolean());
    }

    [Fact]
    public async Task ValidatesCreationInTheDocumentedOrder()
    {
        var author = await api.RegisterAsync("vauth");
        var slug = await CreateArticleAsync(author);

        var anonymous = await api.CreateClient().PostAsync($"/api/v1/articles/{slug}/comments", CommentJson("x"));
        var missing = await author.Client.PostAsync("/api/v1/articles/nope-nope/comments", CommentJson(""));
        var blank = await author.Client.PostAsync($"/api/v1/articles/{slug}/comments", CommentJson("  "));
        var tooLong = await author.Client.PostAsync($"/api/v1/articles/{slug}/comments", CommentJson(new string('a', 1001)));

        Assert.Equal(401, anonymous.Status);
        Assert.Equal(404, missing.Status);
        Assert.Equal(422, blank.Status);
        Assert.Equal("can't be blank", blank.ErrorMessage("body"));
        Assert.Equal("is too long (maximum is 1000 characters)", tooLong.ErrorMessage("body"));
    }

    [Fact]
    public async Task ReturnsNotFoundWhenListingCommentsOfAMissingArticle()
    {
        var response = await api.CreateClient().GetAsync("/api/v1/articles/nope-nope/comments");

        Assert.Equal(404, response.Status);
        Assert.Equal("not found", response.ErrorMessage("article"));
    }

    [Fact]
    public async Task DeletesOnlyOwnComments()
    {
        var author = await api.RegisterAsync("dauth");
        var stranger = await api.RegisterAsync("dstr");
        var slug = await CreateArticleAsync(author);
        var created = await author.Client.PostAsync($"/api/v1/articles/{slug}/comments", CommentJson("mine"));
        var id = created.Property("comment").GetProperty("id").GetInt64();

        var anonymous = await api.CreateClient().DeleteAsync($"/api/v1/articles/{slug}/comments/{id}");
        var foreign = await stranger.Client.DeleteAsync($"/api/v1/articles/{slug}/comments/{id}");
        var deleted = await author.Client.DeleteAsync($"/api/v1/articles/{slug}/comments/{id}");
        var again = await author.Client.DeleteAsync($"/api/v1/articles/{slug}/comments/{id}");

        Assert.Equal(401, anonymous.Status);
        Assert.Equal(403, foreign.Status);
        Assert.Equal("forbidden", foreign.ErrorMessage("comment"));
        Assert.Equal(204, deleted.Status);
        Assert.Equal(404, again.Status);
        Assert.Equal("not found", again.ErrorMessage("comment"));
    }

    [Fact]
    public async Task RejectsMalformedAndMismatchedCommentIds()
    {
        var author = await api.RegisterAsync("iauth");
        var slug = await CreateArticleAsync(author);
        var otherSlug = await CreateArticleAsync(author);
        var created = await author.Client.PostAsync($"/api/v1/articles/{slug}/comments", CommentJson("mine"));
        var id = created.Property("comment").GetProperty("id").GetInt64();

        var notInteger = await author.Client.DeleteAsync($"/api/v1/articles/{slug}/comments/abc");
        var huge = await author.Client.DeleteAsync($"/api/v1/articles/{slug}/comments/{new string('9', 25)}");
        var wrongArticle = await author.Client.DeleteAsync($"/api/v1/articles/{otherSlug}/comments/{id}");
        var missingArticle = await author.Client.DeleteAsync($"/api/v1/articles/nope-nope/comments/{id}");

        Assert.Equal(422, notInteger.Status);
        Assert.Equal("must be an integer", notInteger.ErrorMessage("id"));
        Assert.Equal(404, huge.Status);
        Assert.Equal(404, wrongArticle.Status);
        Assert.Equal(404, missingArticle.Status);
        Assert.Equal("not found", missingArticle.ErrorMessage("article"));
    }

    [Fact]
    public async Task RemovesCommentsWhenTheArticleIsDeleted()
    {
        var author = await api.RegisterAsync("cascade");
        var slug = await CreateArticleAsync(author);
        await author.Client.PostAsync($"/api/v1/articles/{slug}/comments", CommentJson("bye"));

        var deleted = await author.Client.DeleteAsync($"/api/v1/articles/{slug}");
        var comments = await api.CreateClient().GetAsync($"/api/v1/articles/{slug}/comments");

        Assert.Equal(204, deleted.Status);
        Assert.Equal(404, comments.Status);
    }
}
