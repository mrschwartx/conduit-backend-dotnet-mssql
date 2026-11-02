namespace Conduit.Modules.Comments.Domain;

public interface ICommentRepository
{
    Task<Comment> AddAsync(Comment draft, CancellationToken cancellationToken = default);

    Task<Comment?> FindByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Comment>> ListByArticleAsync(Guid articleId, CancellationToken cancellationToken = default);

    Task<bool> DeleteByIdAsync(long id, CancellationToken cancellationToken = default);

    Task DeleteByArticleAsync(Guid articleId, CancellationToken cancellationToken = default);
}
