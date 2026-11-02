using System.Reflection;
using System.Text;

namespace Conduit.SharedKernel.Infrastructure.Persistence;

public sealed record MigrationScript(string Name, Func<string> ReadSql);

public interface IMigrationSource
{
    IEnumerable<MigrationScript> Scripts();
}

public sealed class EmbeddedMigrationSource(Assembly assembly) : IMigrationSource
{
    public const string ResourcePrefix = "migrations/";

    public IEnumerable<MigrationScript> Scripts() =>
        assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                           && name.EndsWith(".sql", StringComparison.Ordinal))
            .Select(name => new MigrationScript(name[ResourcePrefix.Length..], () => Read(name)));

    private string Read(string resource)
    {
        using var stream = assembly.GetManifestResourceStream(resource)
                           ?? throw new InvalidOperationException($"Missing migration resource {resource}");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
