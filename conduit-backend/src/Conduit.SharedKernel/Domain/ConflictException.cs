namespace Conduit.SharedKernel.Domain;

public sealed class ConflictException(IReadOnlyDictionary<string, string[]> errors)
    : DomainException("Conflict", "CONFLICT", errors)
{
    public static ConflictException Fields(IEnumerable<string> fields) =>
        new(fields.ToDictionary(field => field, _ => new[] { Messages.Taken }));
}
