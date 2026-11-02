namespace Conduit.SharedKernel.Application;

public sealed record ProfileDto(string Username, string? Bio, string? Image, bool Following);
