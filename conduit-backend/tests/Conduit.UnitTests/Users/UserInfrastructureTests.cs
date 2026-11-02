using System.Text;
using Conduit.Modules.Users.Infrastructure;
using Conduit.SharedKernel.Domain;
using Conduit.UnitTests.Support;

namespace Conduit.UnitTests.Users;

public sealed class UserInfrastructureTests
{
    private const string Secret = "0123456789abcdef0123456789abcdef";

    private readonly FixedTimeProvider clock = new(Ids.FixedNow);

    private JwtTokenService Service(string secret = Secret, TimeSpan? ttl = null) =>
        new(secret, ttl ?? TimeSpan.FromDays(7), clock);

    [Fact]
    public async Task RoundTripsTheUserId()
    {
        var token = Service().Issue(Ids.User);

        Assert.Equal(Ids.User, await Service().VerifyAsync(token));
    }

    [Fact]
    public void StoresTheUserIdInTheSubjectClaim()
    {
        var token = Service().Issue(Ids.User);
        var payload = Encoding.UTF8.GetString(Convert.FromBase64String(Pad(token.Split('.')[1])));

        Assert.Contains($"\"sub\":\"{Ids.User}\"", payload);
        Assert.Contains("\"exp\":", payload);
    }

    [Fact]
    public async Task RejectsTokensSignedWithAnotherSecret()
    {
        var token = Service("another-secret-another-secret-0000").Issue(Ids.User);

        var error = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Service().VerifyAsync(token));

        Assert.Equal(new[] { "is invalid" }, error.Errors["token"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("a.b.c")]
    public async Task RejectsMalformedTokens(string token)
    {
        var error = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Service().VerifyAsync(token));

        Assert.Equal(new[] { "is invalid" }, error.Errors["token"]);
    }

    [Fact]
    public async Task ReportsExpiredTokensSeparately()
    {
        var token = Service(ttl: TimeSpan.FromHours(1)).Issue(Ids.User);
        clock.Advance(TimeSpan.FromHours(2));

        var error = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Service().VerifyAsync(token));

        Assert.Equal(new[] { "has expired" }, error.Errors["token"]);
    }

    [Fact]
    public async Task AcceptsTokensUntilTheyExpire()
    {
        var token = Service(ttl: TimeSpan.FromHours(1)).Issue(Ids.User);
        clock.Advance(TimeSpan.FromMinutes(59));

        Assert.Equal(Ids.User, await Service().VerifyAsync(token));
    }

    [Fact]
    public async Task HashesAndVerifiesPasswords()
    {
        var hasher = new BcryptPasswordHasher(4);

        var hash = hasher.Hash("password123");

        Assert.NotEqual("password123", hash);
        Assert.True(hasher.Matches("password123", hash));
        Assert.False(hasher.Matches("other-password", hash));
        await Task.CompletedTask;
    }

    private static string Pad(string value)
    {
        var text = value.Replace('-', '+').Replace('_', '/');
        return text.PadRight(text.Length + (4 - text.Length % 4) % 4, '=');
    }
}
