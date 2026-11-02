namespace Conduit.SharedKernel.Infrastructure.Http;

public sealed record ErrorResponse(IReadOnlyDictionary<string, string[]> Errors);
