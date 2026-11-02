namespace Conduit.SharedKernel.Domain;

public sealed class NotFoundException(string resource)
    : DomainException(
        $"{resource} not found",
        "NOT_FOUND",
        new Dictionary<string, string[]> { [resource] = ["not found"] })
{
    public string Resource { get; } = resource;
}
