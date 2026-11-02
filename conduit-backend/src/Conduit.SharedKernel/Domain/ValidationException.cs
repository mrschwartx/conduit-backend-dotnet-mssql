namespace Conduit.SharedKernel.Domain;

public sealed class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : DomainException("Validation failed", "VALIDATION_ERROR", errors)
{
    public static ValidationException Field(string field, params string[] messages) =>
        new(new Dictionary<string, string[]> { [field] = messages });
}
