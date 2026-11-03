using System.Data;
using Conduit.Modules.Comments.Domain;
using Conduit.SharedKernel.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;

namespace Conduit.Modules.Comments.Infrastructure;

public sealed class SqlServerCommentRepository(SqlServerDataSource dataSource) : ICommentRepository
{
    private const string Columns = "id, article_id, author_id, body, created_at, updated_at";

    private const string InsertedColumns =
        "INSERTED.id, INSERTED.article_id, INSERTED.author_id, INSERTED.body, INSERTED.created_at, INSERTED.updated_at";

    public async Task<Comment> AddAsync(Comment draft, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            "INSERT INTO comments (article_id, author_id, body, created_at, updated_at) "
            + $"OUTPUT {InsertedColumns} "
            + "VALUES (@article_id, @author_id, @body, @created_at, @updated_at)");
        command.AddParameter("article_id", SqlDbType.UniqueIdentifier, draft.ArticleId);
        command.AddParameter("author_id", SqlDbType.UniqueIdentifier, draft.AuthorId);
        command.AddParameter("body", SqlDbType.NVarChar, draft.Body.Value);
        command.AddParameter("created_at", SqlDbType.DateTimeOffset, draft.CreatedAt.ToUniversalTime());
        command.AddParameter("updated_at", SqlDbType.DateTimeOffset, draft.UpdatedAt.ToUniversalTime());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return Map(reader);
    }

    public async Task<Comment?> FindByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand($"SELECT {Columns} FROM comments WHERE id = @id");
        command.AddParameter("id", SqlDbType.BigInt, id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<IReadOnlyList<Comment>> ListByArticleAsync(
        Guid articleId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            $"SELECT {Columns} FROM comments WHERE article_id = @article_id ORDER BY created_at DESC, id DESC");
        command.AddParameter("article_id", SqlDbType.UniqueIdentifier, articleId);

        var found = new List<Comment>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            found.Add(Map(reader));
        }

        return found;
    }

    public async Task<bool> DeleteByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand("DELETE FROM comments WHERE id = @id");
        command.AddParameter("id", SqlDbType.BigInt, id);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task DeleteByArticleAsync(Guid articleId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand("DELETE FROM comments WHERE article_id = @article_id");
        command.AddParameter("article_id", SqlDbType.UniqueIdentifier, articleId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Comment Map(SqlDataReader reader) => Comment.Restore(
        reader.GetInt64(0),
        reader.GetGuid(1),
        reader.GetGuid(2),
        CommentBody.Restore(reader.GetString(3)),
        reader.GetDateTimeOffset(4),
        reader.GetDateTimeOffset(5));
}
