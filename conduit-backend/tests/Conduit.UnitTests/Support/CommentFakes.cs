using Conduit.Modules.Comments.Domain;

namespace Conduit.UnitTests.Support;

public sealed class InMemoryCommentRepository : ICommentRepository
{
    private long nextId = 1;

    public List<Comment> Items { get; } = [];

    public Task<Comment> AddAsync(Comment draft, CancellationToken cancellationToken = default)
    {
        var saved = Comment.Restore(nextId++, draft.ArticleId, draft.AuthorId, draft.Body, draft.CreatedAt, draft.UpdatedAt);
        Items.Add(saved);
        return Task.FromResult(saved);
    }

    public Task<Comment?> FindByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(comment => comment.Id == id));

    public Task<IReadOnlyList<Comment>> ListByArticleAsync(Guid articleId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Comment>>(
            Items.Where(comment => comment.ArticleId == articleId).Reverse().ToList());

    public Task<bool> DeleteByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.RemoveAll(comment => comment.Id == id) == 1);

    public Task DeleteByArticleAsync(Guid articleId, CancellationToken cancellationToken = default)
    {
        Items.RemoveAll(comment => comment.ArticleId == articleId);
        return Task.CompletedTask;
    }
}
