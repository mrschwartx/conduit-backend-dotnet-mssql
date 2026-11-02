namespace Conduit.Modules.Users.Domain;

public interface IUserRepository
{
    Guid NextId();

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(User user, CancellationToken cancellationToken = default);

    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> FindByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task<bool> ExistsByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> FindTakenFieldsAsync(
        string? email,
        string? username,
        Guid? excludeId,
        CancellationToken cancellationToken = default);
}
