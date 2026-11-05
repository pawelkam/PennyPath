namespace PennyPath.Api.Tests;

public sealed class TestTimeProvider : TimeProvider
{
    private long utcTicks = DateTimeOffset.UtcNow.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref utcTicks), TimeSpan.Zero);

    public void Advance(TimeSpan interval) => Interlocked.Add(ref utcTicks, interval.Ticks);
}
