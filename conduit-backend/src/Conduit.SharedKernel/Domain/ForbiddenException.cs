namespace Conduit.SharedKernel.Domain;

public sealed class ForbiddenException(string resource)
    : DomainException(
        $"{resource} forbidden",
        "FORBIDDEN",
        new Dictionary<string, string[]> { [resource] = ["forbidden"] })
{
    public string Resource { get; } = resource;
}
