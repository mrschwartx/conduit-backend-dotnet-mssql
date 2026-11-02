using Conduit.Modules.Users.Application;
using Conduit.SharedKernel.Domain;
using Conduit.UnitTests.Support;

namespace Conduit.UnitTests.Users;

public sealed class UserUseCaseTests
{
    private readonly InMemoryUserRepository users = new();

    private readonly InMemoryFollowRepository follows = new();

    private readonly FakePasswordHasher hasher = new();

    private readonly FakeTokenService tokens = new();

    private readonly FixedTimeProvider clock = new(Ids.FixedNow);

    private RegisterUser Register => new(users, hasher, tokens, clock);

    private AuthenticateUser Authenticate => new(users, hasher, tokens);

    private UpdateCurrentUser Update => new(users, hasher, tokens, clock);

    [Fact]
    public async Task RegistersAUserAndReturnsAToken()
    {
        var result = await Register.ExecuteAsync(Input.Of(("username", "john"), ("email", "John@Mail.com"), ("password", "password123")));

        Assert.Equal("john", result.Username);
        Assert.Equal("john@mail.com", result.Email);
        Assert.Null(result.Bio);
        Assert.Null(result.Image);
        Assert.StartsWith("token-", result.Token);
        Assert.Equal("hash:password123", Assert.Single(users.Items).PasswordHash);
    }

    [Fact]
    public async Task ReportsEveryInvalidFieldAtOnce()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(
            () => Register.ExecuteAsync(Input.Of(("username", "a"), ("email", "nope"), ("password", "x"))));

        Assert.Equal(new[] { "username", "email", "password" }, error.Errors.Keys.ToArray());
        Assert.Empty(users.Items);
    }

    [Fact]
    public async Task RejectsMissingFields()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => Register.ExecuteAsync(Input.Empty));

        Assert.Equal(new[] { "can't be blank" }, error.Errors["username"]);
        Assert.Equal(new[] { "can't be blank" }, error.Errors["email"]);
        Assert.Equal(new[] { "can't be blank" }, error.Errors["password"]);
    }

    [Fact]
    public async Task RejectsTakenEmailsAndUsernamesCaseInsensitively()
    {
        users.Seed("john");

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => Register.ExecuteAsync(Input.Of(("username", "JOHN"), ("email", "JOHN@mail.com"), ("password", "password123"))));

        Assert.Equal(new[] { "email", "username" }, error.Errors.Keys.OrderBy(key => key).ToArray());
        Assert.Equal(new[] { "has already been taken" }, error.Errors["email"]);
    }

    [Fact]
    public async Task ReportsOnlyTheTakenField()
    {
        users.Seed("john");

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => Register.ExecuteAsync(Input.Of(("username", "fresh"), ("email", "john@mail.com"), ("password", "password123"))));

        Assert.Equal(new[] { "email" }, error.Errors.Keys.ToArray());
    }

    [Fact]
    public async Task AuthenticatesWithEmailAndPassword()
    {
        var user = users.Seed("john");

        var result = await Authenticate.ExecuteAsync(Input.Of(("email", " JOHN@mail.com "), ("password", "password123")));

        Assert.Equal("john", result.Username);
        Assert.Equal("token-" + user.Id, result.Token);
    }

    [Fact]
    public async Task RejectsBlankCredentialsWithFieldErrors()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(
            () => Authenticate.ExecuteAsync(Input.Of(("email", "  "), ("password", ""))));

        Assert.Equal(new[] { "can't be blank" }, error.Errors["email"]);
        Assert.Equal(new[] { "can't be blank" }, error.Errors["password"]);
    }

    [Fact]
    public async Task RejectsUnknownEmailsAndWrongPasswordsIdentically()
    {
        users.Seed("john");

        var unknown = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => Authenticate.ExecuteAsync(Input.Of(("email", "ghost@mail.com"), ("password", "password123"))));
        var wrong = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => Authenticate.ExecuteAsync(Input.Of(("email", "john@mail.com"), ("password", "wrong-password"))));

        Assert.Equal(unknown.Errors["credentials"], wrong.Errors["credentials"]);
        Assert.Equal(new[] { "are invalid" }, wrong.Errors["credentials"]);
    }

    [Fact]
    public async Task ReturnsTheCurrentUserWithAFreshToken()
    {
        var user = users.Seed("john");

        var result = await new GetCurrentUser(users, tokens).ExecuteAsync(user.Id);

        Assert.Equal("john", result.Username);
        Assert.Equal("token-" + user.Id, result.Token);
    }

    [Fact]
    public async Task TreatsAMissingCurrentUserAsAnInvalidToken()
    {
        var error = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => new GetCurrentUser(users, tokens).ExecuteAsync(Ids.User));

        Assert.Equal(new[] { "is invalid" }, error.Errors["token"]);
    }

    [Fact]
    public async Task RejectsEmptyUpdates()
    {
        var user = users.Seed("john");

        var error = await Assert.ThrowsAsync<ValidationException>(() => Update.ExecuteAsync(user.Id, Input.Empty));
        var unknown = await Assert.ThrowsAsync<ValidationException>(
            () => Update.ExecuteAsync(user.Id, Input.Of(("unknown", "x"))));

        Assert.Equal(new[] { "can't be empty" }, error.Errors["user"]);
        Assert.Equal(new[] { "can't be empty" }, unknown.Errors["user"]);
    }

    [Fact]
    public async Task UpdatesTheProvidedFieldsOnly()
    {
        var user = users.Seed("john");

        var result = await Update.ExecuteAsync(user.Id, Input.Of(("bio", "About me"), ("image", "https://a.co/i.png")));

        Assert.Equal("john", result.Username);
        Assert.Equal("About me", result.Bio);
        Assert.Equal("https://a.co/i.png", result.Image);
        Assert.Equal("hash:password123", user.PasswordHash);
    }

    [Fact]
    public async Task ClearsBioAndImageWithNullOrBlank()
    {
        var user = users.Seed("john");
        await Update.ExecuteAsync(user.Id, Input.Of(("bio", "About me"), ("image", "https://a.co/i.png")));

        var result = await Update.ExecuteAsync(user.Id, Input.Of(("bio", null), ("image", "   ")));

        Assert.Null(result.Bio);
        Assert.Null(result.Image);
    }

    [Fact]
    public async Task ChangesCredentialsAndHashesTheNewPassword()
    {
        var user = users.Seed("john");

        var result = await Update.ExecuteAsync(
            user.Id,
            Input.Of(("username", "johnny"), ("email", "Johnny@Mail.com"), ("password", "new-password-1")));

        Assert.Equal("johnny", result.Username);
        Assert.Equal("johnny@mail.com", result.Email);
        Assert.Equal("hash:new-password-1", user.PasswordHash);
    }

    [Fact]
    public async Task AllowsKeepingYourOwnUsernameAndEmail()
    {
        var user = users.Seed("john");

        var result = await Update.ExecuteAsync(user.Id, Input.Of(("username", "JOHN"), ("email", "john@mail.com")));

        Assert.Equal("JOHN", result.Username);
    }

    [Fact]
    public async Task RejectsCredentialsTakenByAnotherUser()
    {
        var user = users.Seed("john");
        users.Seed("jane");

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => Update.ExecuteAsync(user.Id, Input.Of(("username", "jane"))));

        Assert.Equal(new[] { "username" }, error.Errors.Keys.ToArray());
    }

    [Fact]
    public async Task ValidatesEveryProvidedFieldBeforeChangingAnything()
    {
        var user = users.Seed("john");

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => Update.ExecuteAsync(user.Id, Input.Of(("username", "ab"), ("image", "http://insecure.com/i.png"), ("bio", 5))));

        Assert.Equal(new[] { "username", "bio", "image" }, error.Errors.Keys.ToArray());
        Assert.Equal("john", user.Username.Value);
    }

    [Fact]
    public async Task ShowsProfilesWithTheFollowingFlag()
    {
        var john = users.Seed("john");
        var jane = users.Seed("jane");
        follows.Pairs.Add((john.Id, jane.Id));
        var getProfile = new GetProfile(users, follows);

        var asFollower = await getProfile.ExecuteAsync("JANE", john.Id);
        var asAnonymous = await getProfile.ExecuteAsync("jane", null);

        Assert.Equal("jane", asFollower.Username);
        Assert.True(asFollower.Following);
        Assert.False(asAnonymous.Following);
    }

    [Fact]
    public async Task ReportsMissingProfiles()
    {
        var error = await Assert.ThrowsAsync<NotFoundException>(() => new GetProfile(users, follows).ExecuteAsync("ghost", null));

        Assert.Equal("profile", error.Resource);
    }

    [Fact]
    public async Task FollowsAndUnfollowsIdempotently()
    {
        var john = users.Seed("john");
        users.Seed("jane");
        var follow = new FollowUser(users, follows);
        var unfollow = new UnfollowUser(users, follows);

        var first = await follow.ExecuteAsync(john.Id, "jane");
        var second = await follow.ExecuteAsync(john.Id, "jane");
        var removed = await unfollow.ExecuteAsync(john.Id, "jane");
        var again = await unfollow.ExecuteAsync(john.Id, "jane");

        Assert.True(first.Following);
        Assert.True(second.Following);
        Assert.False(removed.Following);
        Assert.False(again.Following);
        Assert.Empty(follows.Pairs);
    }

    [Fact]
    public async Task RejectsFollowingYourselfAndUnknownUsers()
    {
        var john = users.Seed("john");
        var follow = new FollowUser(users, follows);

        var self = await Assert.ThrowsAsync<ValidationException>(() => follow.ExecuteAsync(john.Id, "john"));
        var missing = await Assert.ThrowsAsync<NotFoundException>(() => follow.ExecuteAsync(john.Id, "ghost"));
        var unfollowMissing = await Assert.ThrowsAsync<NotFoundException>(
            () => new UnfollowUser(users, follows).ExecuteAsync(john.Id, "ghost"));

        Assert.Equal(new[] { "you cannot follow yourself" }, self.Errors["username"]);
        Assert.Equal("profile", missing.Resource);
        Assert.Equal("profile", unfollowMissing.Resource);
    }

    [Fact]
    public async Task VerifiesTokensAgainstExistingUsers()
    {
        var user = users.Seed("john");
        var verify = new VerifyAccessToken(tokens, users);

        var accepted = await verify.ExecuteAsync(tokens.Issue(user.Id));
        var missing = await Assert.ThrowsAsync<AuthenticationFailedException>(() => verify.ExecuteAsync(null));
        var deleted = await Assert.ThrowsAsync<AuthenticationFailedException>(() => verify.ExecuteAsync(tokens.Issue(Ids.OtherUser)));
        var garbage = await Assert.ThrowsAsync<AuthenticationFailedException>(() => verify.ExecuteAsync("garbage"));

        Assert.Equal(user.Id, accepted.UserId);
        Assert.Equal(new[] { "is missing" }, missing.Errors["token"]);
        Assert.Equal(new[] { "is invalid" }, deleted.Errors["token"]);
        Assert.Equal(new[] { "is invalid" }, garbage.Errors["token"]);
    }

    [Fact]
    public async Task ExposesProfilesToOtherModulesThroughTheAuthorDirectory()
    {
        var john = users.Seed("john");
        var jane = users.Seed("jane");
        follows.Pairs.Add((john.Id, jane.Id));
        var directory = new AuthorDirectory(users, follows);

        var profiles = await directory.FindProfilesAsync([jane.Id, jane.Id, john.Id], john.Id);
        var anonymous = await directory.FindProfilesAsync([jane.Id], null);
        var none = await directory.FindProfilesAsync([], john.Id);

        Assert.Equal(2, profiles.Count);
        Assert.True(profiles[jane.Id].Following);
        Assert.False(profiles[john.Id].Following);
        Assert.False(anonymous[jane.Id].Following);
        Assert.Empty(none);
        Assert.Equal(jane.Id, await directory.FindIdByUsernameAsync("JANE"));
        Assert.Null(await directory.FindIdByUsernameAsync("ghost"));
        Assert.Equal(new[] { jane.Id }, (await directory.ListFollowedIdsAsync(john.Id)).ToArray());
    }
}
