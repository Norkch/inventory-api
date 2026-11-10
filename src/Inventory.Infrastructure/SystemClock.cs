using Inventory.Application.Abstractions;

namespace Inventory.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
