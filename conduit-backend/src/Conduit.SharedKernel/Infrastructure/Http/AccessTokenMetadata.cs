namespace Conduit.SharedKernel.Infrastructure.Http;

public enum AccessTokenMode
{
    Required,
    Optional
}

public sealed record AccessTokenMetadata(AccessTokenMode Mode);
