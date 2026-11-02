namespace Conduit.UnitTests.Support;

public static class Input
{
    public static IReadOnlyDictionary<string, object?> Of(params (string Key, object? Value)[] pairs) =>
        pairs.ToDictionary(pair => pair.Key, pair => pair.Value);

    public static IReadOnlyDictionary<string, object?> Empty { get; } = new Dictionary<string, object?>();

    public static List<object?> Tags(params string[] tags) => tags.Cast<object?>().ToList();
}
