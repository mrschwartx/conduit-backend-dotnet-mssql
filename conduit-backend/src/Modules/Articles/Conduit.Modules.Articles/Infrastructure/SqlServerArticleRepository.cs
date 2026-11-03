using System.Data;
using System.Text;
using Conduit.Modules.Articles.Domain;
using Conduit.SharedKernel.Domain;
using Conduit.SharedKernel.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;

namespace Conduit.Modules.Articles.Infrastructure;

public sealed class SqlServerArticleRepository(SqlServerDataSource dataSource) : IArticleRepository
{
    private const string SlugConstraint = "articles_slug_unique";

    private const char TagSeparator = ',';

    private const string TagsSubquery =
        "ISNULL((SELECT STRING_AGG(CAST(t.tag AS NVARCHAR(MAX)), N',') WITHIN GROUP (ORDER BY t.[position]) "
        + "FROM article_tags t WHERE t.article_id = a.id), N'')";

    private const string ViewColumns =
        "a.id, a.author_id, a.slug, a.title, a.description, {body}, " + TagsSubquery + ", a.created_at, a.updated_at, "
        + "(SELECT COUNT(*) FROM favorites f WHERE f.article_id = a.id), "
        + "CAST(CASE WHEN EXISTS (SELECT 1 FROM favorites f WHERE f.article_id = a.id AND f.user_id = @viewer) "
        + "THEN 1 ELSE 0 END AS bit)";

    public Guid NextId() => EntityIds.Generate();

    public async Task AddAsync(Article article, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var insert = connection.CreateCommand(
                         "INSERT INTO articles (id, author_id, slug, title, description, body, created_at, updated_at) "
                         + "VALUES (@id, @author_id, @slug, @title, @description, @body, @created_at, @updated_at)",
                         transaction))
        {
            Bind(insert, article);
            insert.AddParameter("author_id", SqlDbType.UniqueIdentifier, article.AuthorId);
            insert.AddParameter("created_at", SqlDbType.DateTimeOffset, article.CreatedAt.ToUniversalTime());
            await ExecuteGuardingSlugAsync(insert, cancellationToken);
        }

        await InsertTagsAsync(connection, transaction, article, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(Article article, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        int affected;
        await using (var update = connection.CreateCommand(
                         "UPDATE articles SET slug = @slug, title = @title, description = @description, "
                         + "body = @body, updated_at = @updated_at WHERE id = @id",
                         transaction))
        {
            Bind(update, article);
            affected = await ExecuteGuardingSlugAsync(update, cancellationToken);
        }

        if (affected != 1)
        {
            return false;
        }

        await using (var clear = connection.CreateCommand(
                         "DELETE FROM article_tags WHERE article_id = @id",
                         transaction))
        {
            clear.AddParameter("id", SqlDbType.UniqueIdentifier, article.Id);
            await clear.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertTagsAsync(connection, transaction, article, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<Article?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            "SELECT a.id, a.author_id, a.slug, a.title, a.description, a.body, " + TagsSubquery
            + ", a.created_at, a.updated_at FROM articles a WHERE a.slug = @slug");
        command.AddParameter("slug", SqlDbType.NVarChar, slug);

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
            TagList.Restore(ParseTags(reader.GetString(6))),
            reader.GetDateTimeOffset(7),
            reader.GetDateTimeOffset(8));
    }

    public async Task<ArticleView?> FindViewBySlugAsync(
        string slug,
        Guid? viewerId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            "SELECT " + ViewColumns.Replace("{body}", "a.body") + " FROM articles a WHERE a.slug = @slug");
        command.AddParameter("slug", SqlDbType.NVarChar, slug);
        command.AddParameter("viewer", SqlDbType.UniqueIdentifier, viewerId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapView(reader) : null;
    }

    public async Task<ArticlePage> FindPageAsync(ArticleQuery query, CancellationToken cancellationToken = default)
    {
        var conditions = new List<string>();
        var bindings = new List<Action<SqlCommand>>();

        if (query.Tag is not null)
        {
            conditions.Add("EXISTS (SELECT 1 FROM article_tags t WHERE t.article_id = a.id AND t.tag = @tag)");
            bindings.Add(command => command.AddParameter("tag", SqlDbType.NVarChar, query.Tag));
        }

        if (query.AuthorId is { } authorId)
        {
            conditions.Add("a.author_id = @author_id");
            bindings.Add(command => command.AddParameter("author_id", SqlDbType.UniqueIdentifier, authorId));
        }

        if (query.AuthorIds is { } authorIds)
        {
            conditions.Add($"a.author_id IN ({SqlServerExtensions.GuidListQuery("@author_ids")})");
            bindings.Add(command => command.AddGuidList("author_ids", authorIds));
        }

        if (query.FavoritedById is { } favoritedBy)
        {
            conditions.Add("EXISTS (SELECT 1 FROM favorites fv WHERE fv.article_id = a.id AND fv.user_id = @favorited_by)");
            bindings.Add(command => command.AddParameter("favorited_by", SqlDbType.UniqueIdentifier, favoritedBy));
        }

        var where = conditions.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", conditions);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        long total;
        await using (var count = connection.CreateCommand($"SELECT COUNT_BIG(*) FROM articles a{where}"))
        {
            foreach (var bind in bindings)
            {
                bind(count);
            }

            total = Convert.ToInt64(await count.ExecuteScalarAsync(cancellationToken));
        }

        var items = new List<ArticleView>();
        await using (var select = connection.CreateCommand(
                         "SELECT " + ViewColumns.Replace("{body}", "CAST(NULL AS NVARCHAR(MAX))") + $" FROM articles a{where} "
                         + "ORDER BY a.created_at DESC, a.seq DESC OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY"))
        {
            foreach (var bind in bindings)
            {
                bind(select);
            }

            select.AddParameter("viewer", SqlDbType.UniqueIdentifier, query.ViewerId);
            select.AddParameter("limit", SqlDbType.Int, query.Limit);
            select.AddParameter("offset", SqlDbType.BigInt, query.Offset);

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
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            "SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM articles WHERE slug = @slug "
            + "AND (@exclude_id IS NULL OR id <> @exclude_id)) THEN 1 ELSE 0 END AS bit)");
        command.AddParameter("slug", SqlDbType.NVarChar, slug);
        command.AddParameter("exclude_id", SqlDbType.UniqueIdentifier, excludeId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand("DELETE FROM articles WHERE id = @id");
        command.AddParameter("id", SqlDbType.UniqueIdentifier, id);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task FavoriteAsync(Guid articleId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            "INSERT INTO favorites (user_id, article_id) SELECT @user_id, @article_id "
            + "WHERE NOT EXISTS (SELECT 1 FROM favorites WITH (UPDLOCK, HOLDLOCK) "
            + "WHERE user_id = @user_id AND article_id = @article_id)");
        command.AddParameter("user_id", SqlDbType.UniqueIdentifier, userId);
        command.AddParameter("article_id", SqlDbType.UniqueIdentifier, articleId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UnfavoriteAsync(Guid articleId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            "DELETE FROM favorites WHERE user_id = @user_id AND article_id = @article_id");
        command.AddParameter("user_id", SqlDbType.UniqueIdentifier, userId);
        command.AddParameter("article_id", SqlDbType.UniqueIdentifier, articleId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> PopularTagsAsync(int limit, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            "SELECT TOP (@limit) tag FROM article_tags GROUP BY tag "
            + "ORDER BY COUNT(*) DESC, tag COLLATE Latin1_General_100_BIN2 ASC");
        command.AddParameter("limit", SqlDbType.Int, limit);

        var tags = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tags.Add(reader.GetString(0));
        }

        return tags;
    }

    private static void Bind(SqlCommand command, Article article)
    {
        command.AddParameter("id", SqlDbType.UniqueIdentifier, article.Id);
        command.AddParameter("slug", SqlDbType.NVarChar, article.Slug);
        command.AddParameter("title", SqlDbType.NVarChar, article.Title.Value);
        command.AddParameter("description", SqlDbType.NVarChar, article.Description.Value);
        command.AddParameter("body", SqlDbType.NVarChar, article.Body.Value);
        command.AddParameter("updated_at", SqlDbType.DateTimeOffset, article.UpdatedAt.ToUniversalTime());
    }

    private static async Task<int> ExecuteGuardingSlugAsync(SqlCommand command, CancellationToken cancellationToken)
    {
        try
        {
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException error) when (SqlServerErrors.ViolatesConstraint(error, SlugConstraint))
        {
            throw new SlugTakenException();
        }
    }

    private static async Task InsertTagsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Article article,
        CancellationToken cancellationToken)
    {
        var tags = article.TagList.Values;
        if (tags.Count == 0)
        {
            return;
        }

        var rows = new StringBuilder();
        for (var index = 0; index < tags.Count; index++)
        {
            rows.Append(index == 0 ? string.Empty : ", ").Append($"(@article_id, {index}, @tag{index})");
        }

        await using var command = connection.CreateCommand(
            "INSERT INTO article_tags (article_id, [position], tag) VALUES " + rows,
            transaction);
        command.AddParameter("article_id", SqlDbType.UniqueIdentifier, article.Id);
        for (var index = 0; index < tags.Count; index++)
        {
            command.AddParameter($"tag{index}", SqlDbType.NVarChar, tags[index]);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string[] ParseTags(string aggregated) =>
        aggregated.Length == 0 ? [] : aggregated.Split(TagSeparator);

    private static ArticleView MapView(SqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        ParseTags(reader.GetString(6)),
        reader.GetDateTimeOffset(7),
        reader.GetDateTimeOffset(8),
        reader.GetInt32(9),
        reader.GetBoolean(10));
}
