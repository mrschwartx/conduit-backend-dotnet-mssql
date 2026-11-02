namespace Conduit.UnitTests.Support;

public static class Ids
{
    public static readonly Guid User = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public static readonly Guid OtherUser = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public static readonly Guid Article = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    public static readonly DateTimeOffset FixedNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
}
