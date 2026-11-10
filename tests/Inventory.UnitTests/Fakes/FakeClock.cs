using Inventory.Application.Abstractions;

namespace Inventory.UnitTests.Fakes;

public sealed class FakeClock : IClock
{
    public static readonly DateTime FixedUtcNow = new(2026, 11, 9, 14, 3, 0, DateTimeKind.Utc);

    public DateTime UtcNow => FixedUtcNow;
}
