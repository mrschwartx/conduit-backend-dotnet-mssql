using Conduit.Modules.Articles.Domain;
using Conduit.SharedKernel.Application;
using Conduit.SharedKernel.Domain;

namespace Conduit.UnitTests.Support;

public sealed class InMemoryArticleRepository : IArticleRepository
{
    public List<Article> Items { get; } = [];

    public HashSet<(Guid User, Guid Article)> Favorites { get; } = [];

    public int SlugCollisionsToSimulate { get; set; }

    public Guid NextId() => Guid.NewGuid();

    public Task AddAsync(Article article, CancellationToken cancellationToken = default)
    {
        if (SlugCollisionsToSimulate > 0)
        {
            SlugCollisionsToSimulate--;
            throw new SlugTakenException();
        }

        Items.Add(article);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(Article article, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Contains(article));

    public Task<Article?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(article => article.Slug == slug));

    public Task<ArticleView?> FindViewBySlugAsync(string slug, Guid? viewerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Where(article => article.Slug == slug).Select(article => ToView(article, viewerId, true)).FirstOrDefault());

    public Task<ArticlePage> FindPageAsync(ArticleQuery query, CancellationToken cancellationToken = default)
    {
        var matches = Items.AsEnumerable().Reverse()
            .Where(article => query.Tag is null || article.TagList.Values.Contains(query.Tag))
            .Where(article => query.AuthorId is null || article.AuthorId == query.AuthorId)
            .Where(article => query.AuthorIds is null || query.AuthorIds.Contains(article.AuthorId))
            .Where(article => query.FavoritedById is null || Favorites.Contains((query.FavoritedById.Value, article.Id)))
            .ToList();
        var items = matches.Skip((int)query.Offset).Take(query.Limit).Select(article => ToView(article, query.ViewerId, false)).ToList();
        return Task.FromResult(new ArticlePage(items, matches.Count));
    }

    public Task<bool> SlugTakenAsync(string slug, Guid? excludeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Any(article => article.Slug == slug && article.Id != excludeId));

    public Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.RemoveAll(article => article.Id == id) == 1);

    public Task FavoriteAsync(Guid articleId, Guid userId, CancellationToken cancellationToken = default)
    {
        Favorites.Add((userId, articleId));
        return Task.CompletedTask;
    }

    public Task UnfavoriteAsync(Guid articleId, Guid userId, CancellationToken cancellationToken = default)
    {
        Favorites.Remove((userId, articleId));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> PopularTagsAsync(int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(
            Items.SelectMany(article => article.TagList.Values)
                .GroupBy(tag => tag)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .Take(limit)
                .Select(group => group.Key)
                .ToList());

    public Article Seed(Guid authorId, string title, params string[] tags)
    {
        var article = Article.Create(
            NextId(),
            authorId,
            Slug.From(title),
            ArticleTitle.Create(title),
            ArticleDescription.Create("A description"),
            ArticleBody.Create("A body"),
            TagList.Create(Input.Tags(tags)),
            Ids.FixedNow);
        Items.Add(article);
        return article;
    }

    private ArticleView ToView(Article article, Guid? viewerId, bool withBody) => new(
        article.Id,
        article.AuthorId,
        article.Slug,
        article.Title.Value,
        article.Description.Value,
        withBody ? article.Body.Value : null,
        article.TagList.Values,
        article.CreatedAt,
        article.UpdatedAt,
        Favorites.Count(pair => pair.Article == article.Id),
        viewerId is { } viewer && Favorites.Contains((viewer, article.Id)));
}

public sealed class FakeAuthorDirectory : IAuthorDirectory
{
    public Dictionary<Guid, string> Usernames { get; } = [];

    public HashSet<(Guid Follower, Guid Followed)> Follows { get; } = [];

    public FakeAuthorDirectory With(Guid id, string username)
    {
        Usernames[id] = username;
        return this;
    }

    public Task<IReadOnlyDictionary<Guid, ProfileDto>> FindProfilesAsync(
        IEnumerable<Guid> userIds,
        Guid? viewerId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, ProfileDto>>(
            userIds.Distinct().ToDictionary(
                id => id,
                id => new ProfileDto(
                    Usernames[id],
                    null,
                    null,
                    viewerId is { } viewer && Follows.Contains((viewer, id)))));

    public Task<Guid?> FindIdByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        Task.FromResult<Guid?>(
            Usernames.Where(pair => pair.Value == username).Select(pair => (Guid?)pair.Key).FirstOrDefault());

    public Task<IReadOnlyList<Guid>> ListFollowedIdsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>(
            Follows.Where(pair => pair.Follower == userId).Select(pair => pair.Followed).ToList());
}

public sealed class FakeArticleCatalog(InMemoryArticleRepository articles) : IArticleCatalog
{
    public async Task<Guid?> FindIdBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        (await articles.FindBySlugAsync(slug, cancellationToken))?.Id;
}

public sealed class RecordingEventBus : IEventBus
{
    public List<IIntegrationEvent> Published { get; } = [];

    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        Published.Add(integrationEvent);
        return Task.CompletedTask;
    }
}
