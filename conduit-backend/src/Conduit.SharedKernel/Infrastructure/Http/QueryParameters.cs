using Microsoft.AspNetCore.Http;

namespace Conduit.SharedKernel.Infrastructure.Http;

public static class QueryParameters
{
    public static object? Value(IQueryCollection query, string key)
    {
        if (!query.TryGetValue(key, out var values) || values.Count == 0)
        {
            return null;
        }

        return values.Count == 1 ? values[0] : values.ToArray();
    }

    public static IReadOnlyDictionary<string, object?> All(IQueryCollection query) =>
        query.Keys.ToDictionary(key => key, key => Value(query, key), StringComparer.Ordinal);
}
