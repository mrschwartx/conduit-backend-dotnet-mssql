namespace Conduit.SharedKernel.Domain;

public abstract class DomainException(string message, string code, IReadOnlyDictionary<string, string[]> errors)
    : Exception(message)
{
    public string Code { get; } = code;

    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
