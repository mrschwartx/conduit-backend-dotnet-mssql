namespace Conduit.UnitTests.Support;

public sealed class FixedTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset now = start;

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan by) => now = now.Add(by);

    public void Set(DateTimeOffset value) => now = value;
}
