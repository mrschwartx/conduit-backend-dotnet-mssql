namespace Conduit.SharedKernel.Domain;

public static class EntityIds
{
    public static Guid Generate() => Guid.CreateVersion7();

    public static bool IsValid(object? value) => TryParse(value, out _);

    public static bool TryParse(object? value, out Guid id)
    {
        id = Guid.Empty;
        return value is string { Length: 36 } text && Guid.TryParseExact(text, "D", out id);
    }
}
