using Conduit.SharedKernel.Domain;
using Conduit.UnitTests.Support;

namespace Conduit.UnitTests.SharedKernel;

public sealed class TimestampsTests
{
    [Fact]
    public void FormatsUtcWithMilliseconds()
    {
        var value = new DateTimeOffset(2026, 12, 8, 10, 0, 0, TimeSpan.Zero);

        Assert.Equal("2026-12-08T10:00:00.000Z", Timestamps.Format(value));
    }

    [Fact]
    public void ConvertsOffsetsToUtcWhenFormatting()
    {
        var value = new DateTimeOffset(2026, 3, 1, 7, 0, 0, TimeSpan.FromHours(7));

        Assert.Equal("2026-03-01T00:00:00.000Z", Timestamps.Format(value));
    }

    [Fact]
    public void TruncatesToMilliseconds()
    {
        var value = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(1234567);

        var truncated = Timestamps.Truncate(value);

        Assert.Equal(TimeSpan.Zero, truncated.Offset);
        Assert.Equal(0, truncated.Ticks % TimeSpan.TicksPerMillisecond);
        Assert.Equal("2026-01-01T00:00:00.123Z", Timestamps.Format(truncated));
    }

    [Fact]
    public void ReadsTheClockInUtc()
    {
        var clock = new FixedTimeProvider(Ids.FixedNow);

        Assert.Equal(Ids.FixedNow, Timestamps.Now(clock));
    }
}
