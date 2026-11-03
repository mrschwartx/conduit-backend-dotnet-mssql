using System.Data;
using Conduit.Modules.Users.Domain;
using Conduit.SharedKernel.Domain;
using Conduit.SharedKernel.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;

namespace Conduit.Modules.Users.Infrastructure;

public sealed class SqlServerUserRepository(SqlServerDataSource dataSource) : IUserRepository
{
    private const string Columns = "id, username, email, password_hash, bio, image, created_at, updated_at";

    private static readonly IReadOnlyDictionary<string, string> FieldByConstraint = new Dictionary<string, string>
    {
        ["users_email_unique"] = "email",
        ["users_username_unique"] = "username"
    };

    public Guid NextId() => EntityIds.Generate();

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            "INSERT INTO users (id, username, email, password_hash, bio, image, created_at, updated_at) "
            + "VALUES (@id, @username, @email, @password_hash, @bio, @image, @created_at, @updated_at)");
        Bind(command, user);
        command.AddParameter("created_at", SqlDbType.DateTimeOffset, user.CreatedAt.ToUniversalTime());

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException error) when (SqlServerErrors.IsUniqueViolation(error))
        {
            throw TranslateConflict(error);
        }
    }

    public async Task<bool> UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            "UPDATE users SET username = @username, email = @email, password_hash = @password_hash, "
            + "bio = @bio, image = @image, updated_at = @updated_at WHERE id = @id");
        Bind(command, user);

        try
        {
            return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        }
        catch (SqlException error) when (SqlServerErrors.IsUniqueViolation(error))
        {
            throw TranslateConflict(error);
        }
    }

    public async Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand($"SELECT {Columns} FROM users WHERE id = @id");
        command.AddParameter("id", SqlDbType.UniqueIdentifier, id);
        return await ReadSingleAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> FindByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            $"SELECT {Columns} FROM users WHERE id IN ({SqlServerExtensions.GuidListQuery("@ids")})");
        command.AddGuidList("ids", ids);

        var users = new List<User>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(Map(reader));
        }

        return users;
    }

    public async Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            $"SELECT {Columns} FROM users WHERE email_lower = LOWER(@email)");
        command.AddParameter("email", SqlDbType.NVarChar, email);
        return await ReadSingleAsync(command, cancellationToken);
    }

    public async Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            $"SELECT {Columns} FROM users WHERE username_lower = LOWER(@username)");
        command.AddParameter("username", SqlDbType.NVarChar, username);
        return await ReadSingleAsync(command, cancellationToken);
    }

    public async Task<bool> ExistsByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            "SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM users WHERE id = @id) THEN 1 ELSE 0 END AS bit)");
        command.AddParameter("id", SqlDbType.UniqueIdentifier, id);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<IReadOnlyList<string>> FindTakenFieldsAsync(
        string? email,
        string? username,
        Guid? excludeId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            "SELECT "
            + "CAST(CASE WHEN EXISTS (SELECT 1 FROM users WHERE email_lower = LOWER(@email) "
            + "AND (@exclude_id IS NULL OR id <> @exclude_id)) THEN 1 ELSE 0 END AS bit), "
            + "CAST(CASE WHEN EXISTS (SELECT 1 FROM users WHERE username_lower = LOWER(@username) "
            + "AND (@exclude_id IS NULL OR id <> @exclude_id)) THEN 1 ELSE 0 END AS bit)");
        command.AddParameter("email", SqlDbType.NVarChar, email);
        command.AddParameter("username", SqlDbType.NVarChar, username);
        command.AddParameter("exclude_id", SqlDbType.UniqueIdentifier, excludeId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        var taken = new List<string>();
        if (reader.GetBoolean(0))
        {
            taken.Add("email");
        }

        if (reader.GetBoolean(1))
        {
            taken.Add("username");
        }

        return taken;
    }

    private static Exception TranslateConflict(SqlException error)
    {
        foreach (var (constraint, field) in FieldByConstraint)
        {
            if (SqlServerErrors.ViolatesConstraint(error, constraint))
            {
                return ConflictException.Fields([field]);
            }
        }

        return error;
    }

    private static void Bind(SqlCommand command, User user)
    {
        command.AddParameter("id", SqlDbType.UniqueIdentifier, user.Id);
        command.AddParameter("username", SqlDbType.NVarChar, user.Username.Value);
        command.AddParameter("email", SqlDbType.NVarChar, user.Email.Value);
        command.AddParameter("password_hash", SqlDbType.NVarChar, user.PasswordHash);
        command.AddParameter("bio", SqlDbType.NVarChar, user.Bio?.Value);
        command.AddParameter("image", SqlDbType.NVarChar, user.Image?.Value);
        command.AddParameter("updated_at", SqlDbType.DateTimeOffset, user.UpdatedAt.ToUniversalTime());
    }

    private static async Task<User?> ReadSingleAsync(SqlCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    private static User Map(SqlDataReader reader) => User.Restore(
        reader.GetGuid(0),
        Username.Restore(reader.GetString(1)),
        Email.Restore(reader.GetString(2)),
        reader.GetString(3),
        Bio.Restore(reader.IsDBNull(4) ? null : reader.GetString(4)),
        ProfileImage.Restore(reader.IsDBNull(5) ? null : reader.GetString(5)),
        reader.GetDateTimeOffset(6),
        reader.GetDateTimeOffset(7));
}
