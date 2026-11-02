namespace Conduit.SharedKernel.Domain;

public sealed class ErrorCollector
{
    private readonly Dictionary<string, List<string>> errors = new(StringComparer.Ordinal);

    public bool IsEmpty => errors.Count == 0;

    public void Add(string field, string message)
    {
        if (!errors.TryGetValue(field, out var messages))
        {
            messages = [];
            errors[field] = messages;
        }

        messages.Add(message);
    }

    public void Merge(IReadOnlyDictionary<string, string[]> source)
    {
        foreach (var (field, messages) in source)
        {
            foreach (var message in messages)
            {
                Add(field, message);
            }
        }
    }

    public T? Capture<T>(Func<T?> factory)
        where T : class
    {
        try
        {
            return factory();
        }
        catch (ValidationException error)
        {
            Merge(error.Errors);
            return null;
        }
    }

    public void ThrowIfAny()
    {
        if (!IsEmpty)
        {
            throw new ValidationException(errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()));
        }
    }
}
