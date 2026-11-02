using System.Globalization;

namespace Conduit.SharedKernel.Domain;

public static class Timestamps
{
    private const string IsoUtcMillis = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public static DateTimeOffset Now(TimeProvider clock) => Truncate(clock.GetUtcNow());

    public static DateTimeOffset Truncate(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMillisecond, TimeSpan.Zero);
    }

    public static string Format(DateTimeOffset value) =>
        value.ToUniversalTime().ToString(IsoUtcMillis, CultureInfo.InvariantCulture);
}
