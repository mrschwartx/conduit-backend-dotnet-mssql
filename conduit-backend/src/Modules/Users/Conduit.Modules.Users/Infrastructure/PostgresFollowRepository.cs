using Conduit.Modules.Users.Domain;
using Conduit.SharedKernel.Infrastructure.Persistence;
using Npgsql;
using NpgsqlTypes;

namespace Conduit.Modules.Users.Infrastructure;

public sealed class PostgresFollowRepository(NpgsqlDataSource dataSource) : IFollowRepository
{
    public async Task FollowAsync(Guid followerId, Guid followedId, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "INSERT INTO follows (follower_id, followed_id) VALUES (@follower_id, @followed_id) ON CONFLICT DO NOTHING");
        command.AddParameter("follower_id", NpgsqlDbType.Uuid, followerId);
        command.AddParameter("followed_id", NpgsqlDbType.Uuid, followedId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UnfollowAsync(Guid followerId, Guid followedId, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "DELETE FROM follows WHERE follower_id = @follower_id AND followed_id = @followed_id");
        command.AddParameter("follower_id", NpgsqlDbType.Uuid, followerId);
        command.AddParameter("followed_id", NpgsqlDbType.Uuid, followedId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> IsFollowingAsync(
        Guid followerId,
        Guid followedId,
        CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT EXISTS (SELECT 1 FROM follows WHERE follower_id = @follower_id AND followed_id = @followed_id)");
        command.AddParameter("follower_id", NpgsqlDbType.Uuid, followerId);
        command.AddParameter("followed_id", NpgsqlDbType.Uuid, followedId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<IReadOnlySet<Guid>> FindFollowedAmongAsync(
        Guid followerId,
        IReadOnlyCollection<Guid> candidateIds,
        CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT followed_id FROM follows WHERE follower_id = @follower_id AND followed_id = ANY(@candidates)");
        command.AddParameter("follower_id", NpgsqlDbType.Uuid, followerId);
        command.AddParameter("candidates", NpgsqlDbType.Array | NpgsqlDbType.Uuid, candidateIds.ToArray());
        return (await ReadIdsAsync(command, cancellationToken)).ToHashSet();
    }

    public async Task<IReadOnlyList<Guid>> ListFollowedIdsAsync(
        Guid followerId,
        CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT followed_id FROM follows WHERE follower_id = @follower_id");
        command.AddParameter("follower_id", NpgsqlDbType.Uuid, followerId);
        return await ReadIdsAsync(command, cancellationToken);
    }

    private static async Task<List<Guid>> ReadIdsAsync(NpgsqlCommand command, CancellationToken cancellationToken)
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
