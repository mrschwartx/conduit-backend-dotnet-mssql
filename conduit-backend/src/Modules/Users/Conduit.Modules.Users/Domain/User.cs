using Conduit.SharedKernel.Domain;

namespace Conduit.Modules.Users.Domain;

public sealed class User
{
    private User(
        Guid id,
        Username username,
        Email email,
        string passwordHash,
        Bio? bio,
        ProfileImage? image,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        Bio = bio;
        Image = image;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }

    public Username Username { get; private set; }

    public Email Email { get; private set; }

    public string PasswordHash { get; private set; }

    public Bio? Bio { get; private set; }

    public ProfileImage? Image { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static User Register(Guid id, Username username, Email email, string passwordHash, DateTimeOffset now) =>
        new(id, username, email, passwordHash, null, null, now, now);

    public static User Restore(
        Guid id,
        Username username,
        Email email,
        string passwordHash,
        Bio? bio,
        ProfileImage? image,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) =>
        new(id, username, email, passwordHash, bio, image, createdAt, updatedAt);

    public void UpdateProfile(
        Username? username,
        Email? email,
        string? passwordHash,
        Optional<Bio> bio,
        Optional<ProfileImage> image,
        DateTimeOffset now)
    {
        Username = username ?? Username;
        Email = email ?? Email;
        PasswordHash = passwordHash ?? PasswordHash;
        Bio = bio.IsSet ? bio.Value : Bio;
        Image = image.IsSet ? image.Value : Image;
        UpdatedAt = now;
    }
}
