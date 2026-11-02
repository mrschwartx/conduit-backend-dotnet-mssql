using Conduit.Modules.Users.Domain;
using Conduit.SharedKernel.Domain;
using Conduit.UnitTests.Support;

namespace Conduit.UnitTests.Users;

public sealed class UserDomainTests
{
    [Fact]
    public void AcceptsAValidUsername()
    {
        Assert.Equal("john_doe-1", Username.Create("  john_doe-1 ").Value);
    }

    [Theory]
    [InlineData(null, "can't be blank")]
    [InlineData("   ", "can't be blank")]
    [InlineData("ab", "is too short (minimum is 3 characters)")]
    [InlineData("has space", "is invalid")]
    [InlineData("emoji😀", "is invalid")]
    public void RejectsInvalidUsernames(object? raw, string message)
    {
        var error = Assert.Throws<ValidationException>(() => Username.Create(raw));

        Assert.Equal(new[] { message }, error.Errors["username"]);
    }

    [Fact]
    public void RejectsLongUsernames()
    {
        var error = Assert.Throws<ValidationException>(() => Username.Create(new string('a', 31)));

        Assert.Equal(new[] { "is too long (maximum is 30 characters)" }, error.Errors["username"]);
    }

    [Fact]
    public void LowercasesAndTrimsEmails()
    {
        Assert.Equal("john@mail.com", Email.Create("  John@Mail.COM ").Value);
    }

    [Theory]
    [InlineData(null, "can't be blank")]
    [InlineData("", "can't be blank")]
    [InlineData("not-an-email", "is invalid")]
    [InlineData("a@b", "is invalid")]
    [InlineData("a b@c.com", "is invalid")]
    public void RejectsInvalidEmails(object? raw, string message)
    {
        var error = Assert.Throws<ValidationException>(() => Email.Create(raw));

        Assert.Equal(new[] { message }, error.Errors["email"]);
    }

    [Fact]
    public void RejectsOverlongEmails()
    {
        var error = Assert.Throws<ValidationException>(() => Email.Create(new string('a', 250) + "@b.co"));

        Assert.Equal(new[] { "is too long (maximum is 254 characters)" }, error.Errors["email"]);
    }

    [Fact]
    public void KeepsPasswordsUntrimmed()
    {
        Assert.Equal("  secret  ", PlainPassword.Create("  secret  ").Value);
    }

    [Theory]
    [InlineData(null, "can't be blank")]
    [InlineData("", "can't be blank")]
    [InlineData("short", "is too short (minimum is 8 characters)")]
    public void RejectsInvalidPasswords(object? raw, string message)
    {
        var error = Assert.Throws<ValidationException>(() => PlainPassword.Create(raw));

        Assert.Equal(new[] { message }, error.Errors["password"]);
    }

    [Fact]
    public void LimitsPasswordsByCharactersAndBytes()
    {
        var tooManyCharacters = Assert.Throws<ValidationException>(() => PlainPassword.Create(new string('a', 73)));
        var tooManyBytes = Assert.Throws<ValidationException>(() => PlainPassword.Create(new string('é', 40)));

        Assert.Equal(new[] { "is too long (maximum is 72 characters)" }, tooManyCharacters.Errors["password"]);
        Assert.Equal(new[] { "is too long (maximum is 72 bytes)" }, tooManyBytes.Errors["password"]);
        Assert.Equal(72, PlainPassword.Create(new string('a', 72)).Value.Length);
    }

    [Fact]
    public void HidesThePasswordInToString()
    {
        Assert.DoesNotContain("secret", PlainPassword.Create("secret123").ToString());
    }

    [Fact]
    public void TreatsBlankBiosAndImagesAsAbsent()
    {
        Assert.Null(Bio.Create(null));
        Assert.Null(Bio.Create("   "));
        Assert.Null(ProfileImage.Create(null));
        Assert.Null(ProfileImage.Create(" "));
    }

    [Fact]
    public void TrimsBios()
    {
        Assert.Equal("Hello", Bio.Create("  Hello ")!.Value);
    }

    [Fact]
    public void RejectsInvalidBios()
    {
        var notText = Assert.Throws<ValidationException>(() => Bio.Create(5));
        var tooLong = Assert.Throws<ValidationException>(() => Bio.Create(new string('a', 301)));

        Assert.Equal(new[] { "is invalid" }, notText.Errors["bio"]);
        Assert.Equal(new[] { "is too long (maximum is 300 characters)" }, tooLong.Errors["bio"]);
    }

    [Fact]
    public void AcceptsHttpsImages()
    {
        Assert.Equal("https://cdn.example.com/a.png", ProfileImage.Create("https://cdn.example.com/a.png")!.Value);
    }

    [Theory]
    [InlineData("http://example.com/a.png")]
    [InlineData("ftp://example.com/a.png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a url")]
    [InlineData("https://")]
    public void RejectsNonHttpsImages(string raw)
    {
        var error = Assert.Throws<ValidationException>(() => ProfileImage.Create(raw));

        Assert.Equal(new[] { "must be a valid https URL" }, error.Errors["image"]);
    }

    [Fact]
    public void RejectsNonStringAndOverlongImages()
    {
        var notText = Assert.Throws<ValidationException>(() => ProfileImage.Create(5));
        var tooLong = Assert.Throws<ValidationException>(() => ProfileImage.Create("https://a.co/" + new string('a', 2048)));

        Assert.Equal(new[] { "is invalid" }, notText.Errors["image"]);
        Assert.Equal(new[] { "is too long (maximum is 2048 characters)" }, tooLong.Errors["image"]);
    }

    [Fact]
    public void RegistersUsersWithoutBioOrImage()
    {
        var user = User.Register(Ids.User, Username.Create("john"), Email.Create("john@mail.com"), "hash", Ids.FixedNow);

        Assert.Null(user.Bio);
        Assert.Null(user.Image);
        Assert.Equal(user.CreatedAt, user.UpdatedAt);
    }

    [Fact]
    public void UpdatesOnlyTheProvidedFields()
    {
        var user = User.Register(Ids.User, Username.Create("john"), Email.Create("john@mail.com"), "hash", Ids.FixedNow);
        var later = Ids.FixedNow.AddHours(1);

        user.UpdateProfile(null, null, null, Optional<Bio>.Of(Bio.Create("About me")), Optional<ProfileImage>.Unset, later);

        Assert.Equal("john", user.Username.Value);
        Assert.Equal("hash", user.PasswordHash);
        Assert.Equal("About me", user.Bio!.Value);
        Assert.Null(user.Image);
        Assert.Equal(later, user.UpdatedAt);
        Assert.Equal(Ids.FixedNow, user.CreatedAt);
    }

    [Fact]
    public void ClearsBioAndImageWhenExplicitlySetToNothing()
    {
        var user = User.Restore(
            Ids.User,
            Username.Create("john"),
            Email.Create("john@mail.com"),
            "hash",
            Bio.Create("About me"),
            ProfileImage.Create("https://a.co/i.png"),
            Ids.FixedNow,
            Ids.FixedNow);

        user.UpdateProfile(null, null, null, Optional<Bio>.Of(null), Optional<ProfileImage>.Of(null), Ids.FixedNow);

        Assert.Null(user.Bio);
        Assert.Null(user.Image);
    }
}
