using Conduit.Modules.Comments.Domain;
using Conduit.SharedKernel.Infrastructure.Persistence;
using Npgsql;
using NpgsqlTypes;

namespace Conduit.Modules.Comments.Infrastructure;

public sealed class PostgresCommentRepository(NpgsqlDataSource dataSource) : ICommentRepository
{
    private const string Columns = "id, article_id, author_id, body, created_at, updated_at";

    public async Task<Comment> AddAsync(Comment draft, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "INSERT INTO comments (article_id, author_id, body, created_at, updated_at) "
            + $"VALUES (@article_id, @author_id, @body, @created_at, @updated_at) RETURNING {Columns}");
        command.AddParameter("article_id", NpgsqlDbType.Uuid, draft.ArticleId);
        command.AddParameter("author_id", NpgsqlDbType.Uuid, draft.AuthorId);
        command.AddParameter("body", NpgsqlDbType.Text, draft.Body.Value);
        command.AddParameter("created_at", NpgsqlDbType.TimestampTz, draft.CreatedAt.ToUniversalTime());
        command.AddParameter("updated_at", NpgsqlDbType.TimestampTz, draft.UpdatedAt.ToUniversalTime());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return Map(reader);
    }

    public async Task<Comment?> FindByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand($"SELECT {Columns} FROM comments WHERE id = @id");
        command.AddParameter("id", NpgsqlDbType.Bigint, id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<IReadOnlyList<Comment>> ListByArticleAsync(
        Guid articleId,
        CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            $"SELECT {Columns} FROM comments WHERE article_id = @article_id ORDER BY created_at DESC, id DESC");
        command.AddParameter("article_id", NpgsqlDbType.Uuid, articleId);

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
        await using var command = dataSource.CreateCommand("DELETE FROM comments WHERE id = @id");
        command.AddParameter("id", NpgsqlDbType.Bigint, id);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task DeleteByArticleAsync(Guid articleId, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("DELETE FROM comments WHERE article_id = @article_id");
        command.AddParameter("article_id", NpgsqlDbType.Uuid, articleId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Comment Map(NpgsqlDataReader reader) => Comment.Restore(
        reader.GetInt64(0),
        reader.GetGuid(1),
        reader.GetGuid(2),
        CommentBody.Restore(reader.GetString(3)),
        reader.GetFieldValue<DateTimeOffset>(4),
        reader.GetFieldValue<DateTimeOffset>(5));
}
