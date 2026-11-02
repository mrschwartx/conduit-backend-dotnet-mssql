using System.Text;
using Conduit.Modules.Users.Domain;
using Conduit.SharedKernel.Domain;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Conduit.Modules.Users.Infrastructure;

public sealed class JwtTokenService : ITokenService
{
    private const string SubjectClaim = "sub";

    private readonly SymmetricSecurityKey key;

    private readonly TimeSpan ttl;

    private readonly TimeProvider clock;

    private readonly JsonWebTokenHandler handler = new();

    public JwtTokenService(string secret, TimeSpan ttl, TimeProvider clock)
    {
        key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        this.ttl = ttl;
        this.clock = clock;
    }

    public string Issue(Guid userId)
    {
        var now = clock.GetUtcNow();
        var descriptor = new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object> { [SubjectClaim] = userId.ToString() },
            IssuedAt = now.UtcDateTime,
            Expires = now.Add(ttl).UtcDateTime,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };
        return handler.CreateToken(descriptor);
    }

    public async Task<Guid> VerifyAsync(string token, CancellationToken cancellationToken = default)
    {
        TokenValidationResult result;
        try
        {
            result = await handler.ValidateTokenAsync(token, CreateParameters());
        }
        catch (Exception error) when (error is ArgumentException or SecurityTokenException)
        {
            throw AuthenticationFailedException.InvalidToken();
        }

        if (!result.IsValid || result.SecurityToken is not JsonWebToken jwt)
        {
            throw AuthenticationFailedException.InvalidToken();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (jwt.ValidTo == DateTime.MinValue)
        {
            throw AuthenticationFailedException.InvalidToken();
        }

        if (jwt.ValidTo <= now)
        {
            throw AuthenticationFailedException.ExpiredToken();
        }

        if (!result.Claims.TryGetValue(SubjectClaim, out var raw) || !EntityIds.TryParse(raw, out var userId))
        {
            throw AuthenticationFailedException.InvalidToken();
        }

        return userId;
    }

    private TokenValidationParameters CreateParameters() => new()
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = false,
        RequireSignedTokens = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = key,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
    };
}
