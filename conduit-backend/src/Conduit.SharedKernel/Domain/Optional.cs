namespace Conduit.SharedKernel.Domain;

public readonly record struct Optional<T>(bool IsSet, T? Value)
{
    public static Optional<T> Unset => default;

    public static Optional<T> Of(T? value) => new(true, value);
}
