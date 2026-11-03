using System.Data;
using Conduit.Modules.Users.Domain;
using Conduit.SharedKernel.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;

namespace Conduit.Modules.Users.Infrastructure;

public sealed class SqlServerFollowRepository(SqlServerDataSource dataSource) : IFollowRepository
{
    public async Task FollowAsync(Guid followerId, Guid followedId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            "INSERT INTO follows (follower_id, followed_id) SELECT @follower_id, @followed_id "
            + "WHERE NOT EXISTS (SELECT 1 FROM follows WITH (UPDLOCK, HOLDLOCK) "
            + "WHERE follower_id = @follower_id AND followed_id = @followed_id)");
        command.AddParameter("follower_id", SqlDbType.UniqueIdentifier, followerId);
        command.AddParameter("followed_id", SqlDbType.UniqueIdentifier, followedId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UnfollowAsync(Guid followerId, Guid followedId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            "DELETE FROM follows WHERE follower_id = @follower_id AND followed_id = @followed_id");
        command.AddParameter("follower_id", SqlDbType.UniqueIdentifier, followerId);
        command.AddParameter("followed_id", SqlDbType.UniqueIdentifier, followedId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> IsFollowingAsync(
        Guid followerId,
        Guid followedId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            "SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM follows "
            + "WHERE follower_id = @follower_id AND followed_id = @followed_id) THEN 1 ELSE 0 END AS bit)");
        command.AddParameter("follower_id", SqlDbType.UniqueIdentifier, followerId);
        command.AddParameter("followed_id", SqlDbType.UniqueIdentifier, followedId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<IReadOnlySet<Guid>> FindFollowedAmongAsync(
        Guid followerId,
        IReadOnlyCollection<Guid> candidateIds,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            "SELECT followed_id FROM follows WHERE follower_id = @follower_id "
            + $"AND followed_id IN ({SqlServerExtensions.GuidListQuery("@candidates")})");
        command.AddParameter("follower_id", SqlDbType.UniqueIdentifier, followerId);
        command.AddGuidList("candidates", candidateIds);
        return (await ReadIdsAsync(command, cancellationToken)).ToHashSet();
    }

    public async Task<IReadOnlyList<Guid>> ListFollowedIdsAsync(
        Guid followerId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand(
            "SELECT followed_id FROM follows WHERE follower_id = @follower_id");
        command.AddParameter("follower_id", SqlDbType.UniqueIdentifier, followerId);
        return await ReadIdsAsync(command, cancellationToken);
    }

    private static async Task<List<Guid>> ReadIdsAsync(SqlCommand command, CancellationToken cancellationToken)
    {
        var ids = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }
}
