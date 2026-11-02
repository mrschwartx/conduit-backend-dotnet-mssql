using Conduit.Modules.Users.Domain;
using Conduit.SharedKernel.Domain;
using Conduit.SharedKernel.Infrastructure.Persistence;
using Npgsql;
using NpgsqlTypes;

namespace Conduit.Modules.Users.Infrastructure;

public sealed class PostgresUserRepository(NpgsqlDataSource dataSource) : IUserRepository
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
        await using var command = dataSource.CreateCommand(
            "INSERT INTO users (id, username, email, password_hash, bio, image, created_at, updated_at) "
            + "VALUES (@id, @username, @email, @password_hash, @bio, @image, @created_at, @updated_at)");
        Bind(command, user);
        command.AddParameter("created_at", NpgsqlDbType.TimestampTz, user.CreatedAt.ToUniversalTime());

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException error) when (PostgresErrors.IsUniqueViolation(error))
        {
            throw TranslateConflict(error);
        }
    }

    public async Task<bool> UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE users SET username = @username, email = @email, password_hash = @password_hash, "
            + "bio = @bio, image = @image, updated_at = @updated_at WHERE id = @id");
        Bind(command, user);

        try
        {
            return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        }
        catch (PostgresException error) when (PostgresErrors.IsUniqueViolation(error))
        {
            throw TranslateConflict(error);
        }
    }

    public async Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand($"SELECT {Columns} FROM users WHERE id = @id");
        command.AddParameter("id", NpgsqlDbType.Uuid, id);
        return await ReadSingleAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> FindByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand($"SELECT {Columns} FROM users WHERE id = ANY(@ids)");
        command.AddParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, ids.ToArray());

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
        await using var command = dataSource.CreateCommand(
            $"SELECT {Columns} FROM users WHERE lower(email) = lower(@email)");
        command.AddParameter("email", NpgsqlDbType.Text, email);
        return await ReadSingleAsync(command, cancellationToken);
    }

    public async Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            $"SELECT {Columns} FROM users WHERE lower(username) = lower(@username)");
        command.AddParameter("username", NpgsqlDbType.Text, username);
        return await ReadSingleAsync(command, cancellationToken);
    }

    public async Task<bool> ExistsByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("SELECT EXISTS (SELECT 1 FROM users WHERE id = @id)");
        command.AddParameter("id", NpgsqlDbType.Uuid, id);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<IReadOnlyList<string>> FindTakenFieldsAsync(
        string? email,
        string? username,
        Guid? excludeId,
        CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT COALESCE(bool_or(lower(email) = lower(@email)), false), "
            + "COALESCE(bool_or(lower(username) = lower(@username)), false) "
            + "FROM users WHERE (@exclude_id IS NULL OR id <> @exclude_id)");
        command.AddParameter("email", NpgsqlDbType.Text, email);
        command.AddParameter("username", NpgsqlDbType.Text, username);
        command.AddParameter("exclude_id", NpgsqlDbType.Uuid, excludeId);

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

    private static Exception TranslateConflict(PostgresException error) =>
        error.ConstraintName is not null && FieldByConstraint.TryGetValue(error.ConstraintName, out var field)
            ? ConflictException.Fields([field])
            : error;

    private static void Bind(NpgsqlCommand command, User user)
    {
        command.AddParameter("id", NpgsqlDbType.Uuid, user.Id);
        command.AddParameter("username", NpgsqlDbType.Text, user.Username.Value);
        command.AddParameter("email", NpgsqlDbType.Text, user.Email.Value);
        command.AddParameter("password_hash", NpgsqlDbType.Text, user.PasswordHash);
        command.AddParameter("bio", NpgsqlDbType.Text, user.Bio?.Value);
        command.AddParameter("image", NpgsqlDbType.Text, user.Image?.Value);
        command.AddParameter("updated_at", NpgsqlDbType.TimestampTz, user.UpdatedAt.ToUniversalTime());
    }

    private static async Task<User?> ReadSingleAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    private static User Map(NpgsqlDataReader reader) => User.Restore(
        reader.GetGuid(0),
        Username.Restore(reader.GetString(1)),
        Email.Restore(reader.GetString(2)),
        reader.GetString(3),
        Bio.Restore(reader.IsDBNull(4) ? null : reader.GetString(4)),
        ProfileImage.Restore(reader.IsDBNull(5) ? null : reader.GetString(5)),
        reader.GetFieldValue<DateTimeOffset>(6),
        reader.GetFieldValue<DateTimeOffset>(7));
}
