using Conduit.Modules.Users.Domain;
using Conduit.SharedKernel.Domain;

namespace Conduit.UnitTests.Support;

public sealed class InMemoryUserRepository : IUserRepository
{
    public List<User> Items { get; } = [];

    public Guid NextId() => Guid.NewGuid();

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        Items.Add(user);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(User user, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Any(item => item.Id == user.Id));

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(user => user.Id == id));

    public Task<IReadOnlyList<User>> FindByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<User>>(Items.Where(user => ids.Contains(user.Id)).ToList());

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(user => Same(user.Email.Value, email)));

    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(user => Same(user.Username.Value, username)));

    public Task<bool> ExistsByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Any(user => user.Id == id));

    public Task<IReadOnlyList<string>> FindTakenFieldsAsync(
        string? email,
        string? username,
        Guid? excludeId,
        CancellationToken cancellationToken = default)
    {
        var others = Items.Where(user => user.Id != excludeId).ToList();
        var taken = new List<string>();
        if (email is not null && others.Any(user => Same(user.Email.Value, email)))
        {
            taken.Add("email");
        }

        if (username is not null && others.Any(user => Same(user.Username.Value, username)))
        {
            taken.Add("username");
        }

        return Task.FromResult<IReadOnlyList<string>>(taken);
    }

    public User Seed(string username, string email = "", string password = "password123")
    {
        var user = User.Register(
            NextId(),
            Username.Create(username),
            Email.Create(email.Length == 0 ? $"{username}@mail.com" : email),
            new FakePasswordHasher().Hash(password),
            Ids.FixedNow);
        Items.Add(user);
        return user;
    }

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}

public sealed class InMemoryFollowRepository : IFollowRepository
{
    public HashSet<(Guid Follower, Guid Followed)> Pairs { get; } = [];

    public Task FollowAsync(Guid followerId, Guid followedId, CancellationToken cancellationToken = default)
    {
        Pairs.Add((followerId, followedId));
        return Task.CompletedTask;
    }

    public Task UnfollowAsync(Guid followerId, Guid followedId, CancellationToken cancellationToken = default)
    {
        Pairs.Remove((followerId, followedId));
        return Task.CompletedTask;
    }

    public Task<bool> IsFollowingAsync(Guid followerId, Guid followedId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Pairs.Contains((followerId, followedId)));

    public Task<IReadOnlySet<Guid>> FindFollowedAmongAsync(
        Guid followerId,
        IReadOnlyCollection<Guid> candidateIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<Guid>>(
            Pairs.Where(pair => pair.Follower == followerId && candidateIds.Contains(pair.Followed))
                .Select(pair => pair.Followed)
                .ToHashSet());

    public Task<IReadOnlyList<Guid>> ListFollowedIdsAsync(Guid followerId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>(
            Pairs.Where(pair => pair.Follower == followerId).Select(pair => pair.Followed).ToList());
}

public sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string plain) => "hash:" + plain;

    public bool Matches(string plain, string hash) => hash == "hash:" + plain;
}

public sealed class FakeTokenService : ITokenService
{
    private const string Prefix = "token-";

    public string Issue(Guid userId) => Prefix + userId;

    public Task<Guid> VerifyAsync(string token, CancellationToken cancellationToken = default) =>
        token.StartsWith(Prefix, StringComparison.Ordinal) && Guid.TryParse(token[Prefix.Length..], out var id)
            ? Task.FromResult(id)
            : throw AuthenticationFailedException.InvalidToken();
}
