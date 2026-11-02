using Conduit.Modules.Articles.Domain;
using Conduit.SharedKernel.Domain;
using Conduit.SharedKernel.Infrastructure.Persistence;
using Npgsql;
using NpgsqlTypes;

namespace Conduit.Modules.Articles.Infrastructure;

public sealed class PostgresArticleRepository(NpgsqlDataSource dataSource) : IArticleRepository
{
    private const string SlugConstraint = "articles_slug_unique";

    private const string TagsSubquery =
        "COALESCE((SELECT array_agg(t.tag ORDER BY t.position) FROM article_tags t WHERE t.article_id = a.id), "
        + "ARRAY[]::varchar[])";

    private const string ViewColumns =
        "a.id, a.author_id, a.slug, a.title, a.description, {body}, " + TagsSubquery + ", a.created_at, a.updated_at, "
        + "(SELECT count(*)::int FROM favorites f WHERE f.article_id = a.id), "
        + "EXISTS (SELECT 1 FROM favorites f WHERE f.article_id = a.id AND f.user_id = @viewer)";

    public Guid NextId() => EntityIds.Generate();

    public async Task AddAsync(Article article, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var insert = new NpgsqlCommand(
                         "INSERT INTO articles (id, author_id, slug, title, description, body, created_at, updated_at) "
                         + "VALUES (@id, @author_id, @slug, @title, @description, @body, @created_at, @updated_at)",
                         connection,
                         transaction))
        {
            Bind(insert, article);
            insert.AddParameter("author_id", NpgsqlDbType.Uuid, article.AuthorId);
            insert.AddParameter("created_at", NpgsqlDbType.TimestampTz, article.CreatedAt.ToUniversalTime());
            await ExecuteGuardingSlugAsync(insert, cancellationToken);
        }

        await InsertTagsAsync(connection, transaction, article, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(Article article, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        int affected;
        await using (var update = new NpgsqlCommand(
                         "UPDATE articles SET slug = @slug, title = @title, description = @description, "
                         + "body = @body, updated_at = @updated_at WHERE id = @id",
                         connection,
                         transaction))
        {
            Bind(update, article);
            affected = await ExecuteGuardingSlugAsync(update, cancellationToken);
        }

        if (affected != 1)
        {
            return false;
        }

        await using (var clear = new NpgsqlCommand("DELETE FROM article_tags WHERE article_id = @id", connection, transaction))
        {
            clear.AddParameter("id", NpgsqlDbType.Uuid, article.Id);
            await clear.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertTagsAsync(connection, transaction, article, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<Article?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT a.id, a.author_id, a.slug, a.title, a.description, a.body, " + TagsSubquery
            + ", a.created_at, a.updated_at FROM articles a WHERE a.slug = @slug");
        command.AddParameter("slug", NpgsqlDbType.Text, slug);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Article.Restore(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            ArticleTitle.Restore(reader.GetString(3)),
            ArticleDescription.Restore(reader.GetString(4)),
            ArticleBody.Restore(reader.GetString(5)),
            TagList.Restore(reader.GetFieldValue<string[]>(6)),
            reader.GetFieldValue<DateTimeOffset>(7),
            reader.GetFieldValue<DateTimeOffset>(8));
    }

    public async Task<ArticleView?> FindViewBySlugAsync(
        string slug,
        Guid? viewerId,
        CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT " + ViewColumns.Replace("{body}", "a.body") + " FROM articles a WHERE a.slug = @slug");
        command.AddParameter("slug", NpgsqlDbType.Text, slug);
        command.AddParameter("viewer", NpgsqlDbType.Uuid, viewerId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapView(reader) : null;
    }

    public async Task<ArticlePage> FindPageAsync(ArticleQuery query, CancellationToken cancellationToken = default)
    {
        var conditions = new List<string>();
        var bindings = new List<(string Name, NpgsqlDbType Type, object? Value)>();

        if (query.Tag is not null)
        {
            conditions.Add("EXISTS (SELECT 1 FROM article_tags t WHERE t.article_id = a.id AND t.tag = @tag)");
            bindings.Add(("tag", NpgsqlDbType.Text, query.Tag));
        }

        if (query.AuthorId is { } authorId)
        {
            conditions.Add("a.author_id = @author_id");
            bindings.Add(("author_id", NpgsqlDbType.Uuid, authorId));
        }

        if (query.AuthorIds is { } authorIds)
        {
            conditions.Add("a.author_id = ANY(@author_ids)");
            bindings.Add(("author_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, authorIds.ToArray()));
        }

        if (query.FavoritedById is { } favoritedBy)
        {
            conditions.Add("EXISTS (SELECT 1 FROM favorites fv WHERE fv.article_id = a.id AND fv.user_id = @favorited_by)");
            bindings.Add(("favorited_by", NpgsqlDbType.Uuid, favoritedBy));
        }

        var where = conditions.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", conditions);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        long total;
        await using (var count = new NpgsqlCommand($"SELECT count(*) FROM articles a{where}", connection))
        {
            foreach (var (name, type, value) in bindings)
            {
                count.AddParameter(name, type, value);
            }

            total = Convert.ToInt64(await count.ExecuteScalarAsync(cancellationToken));
        }

        var items = new List<ArticleView>();
        await using (var select = new NpgsqlCommand(
                         "SELECT " + ViewColumns.Replace("{body}", "NULL::text") + $" FROM articles a{where} "
                         + "ORDER BY a.created_at DESC, a.seq DESC LIMIT @limit OFFSET @offset",
                         connection))
        {
            foreach (var (name, type, value) in bindings)
            {
                select.AddParameter(name, type, value);
            }

            select.AddParameter("viewer", NpgsqlDbType.Uuid, query.ViewerId);
            select.AddParameter("limit", NpgsqlDbType.Integer, query.Limit);
            select.AddParameter("offset", NpgsqlDbType.Bigint, query.Offset);

            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(MapView(reader));
            }
        }

        return new ArticlePage(items, total);
    }

    public async Task<bool> SlugTakenAsync(string slug, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT EXISTS (SELECT 1 FROM articles WHERE slug = @slug AND (@exclude_id IS NULL OR id <> @exclude_id))");
        command.AddParameter("slug", NpgsqlDbType.Text, slug);
        command.AddParameter("exclude_id", NpgsqlDbType.Uuid, excludeId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("DELETE FROM articles WHERE id = @id");
        command.AddParameter("id", NpgsqlDbType.Uuid, id);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task FavoriteAsync(Guid articleId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "INSERT INTO favorites (user_id, article_id) VALUES (@user_id, @article_id) ON CONFLICT DO NOTHING");
        command.AddParameter("user_id", NpgsqlDbType.Uuid, userId);
        command.AddParameter("article_id", NpgsqlDbType.Uuid, articleId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UnfavoriteAsync(Guid articleId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "DELETE FROM favorites WHERE user_id = @user_id AND article_id = @article_id");
        command.AddParameter("user_id", NpgsqlDbType.Uuid, userId);
        command.AddParameter("article_id", NpgsqlDbType.Uuid, articleId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> PopularTagsAsync(int limit, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT tag FROM article_tags GROUP BY tag ORDER BY count(*) DESC, tag COLLATE \"C\" ASC LIMIT @limit");
        command.AddParameter("limit", NpgsqlDbType.Integer, limit);

        var tags = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tags.Add(reader.GetString(0));
        }

        return tags;
    }

    private static void Bind(NpgsqlCommand command, Article article)
    {
        command.AddParameter("id", NpgsqlDbType.Uuid, article.Id);
        command.AddParameter("slug", NpgsqlDbType.Text, article.Slug);
        command.AddParameter("title", NpgsqlDbType.Text, article.Title.Value);
        command.AddParameter("description", NpgsqlDbType.Text, article.Description.Value);
        command.AddParameter("body", NpgsqlDbType.Text, article.Body.Value);
        command.AddParameter("updated_at", NpgsqlDbType.TimestampTz, article.UpdatedAt.ToUniversalTime());
    }

    private static async Task<int> ExecuteGuardingSlugAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        try
        {
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException error) when (PostgresErrors.IsUniqueViolation(error) && error.ConstraintName == SlugConstraint)
        {
            throw new SlugTakenException();
        }
    }

    private static async Task InsertTagsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Article article,
        CancellationToken cancellationToken)
    {
        if (article.TagList.Values.Count == 0)
        {
            return;
        }

        await using var command = new NpgsqlCommand(
            "INSERT INTO article_tags (article_id, position, tag) "
            + "SELECT @article_id, (ordinality - 1)::smallint, tag FROM unnest(@tags) WITH ORDINALITY AS t(tag, ordinality)",
            connection,
            transaction);
        command.AddParameter("article_id", NpgsqlDbType.Uuid, article.Id);
        command.AddParameter("tags", NpgsqlDbType.Array | NpgsqlDbType.Text, article.TagList.Values.ToArray());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static ArticleView MapView(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.GetFieldValue<string[]>(6),
        reader.GetFieldValue<DateTimeOffset>(7),
        reader.GetFieldValue<DateTimeOffset>(8),
        reader.GetInt32(9),
        reader.GetBoolean(10));
}
