using System.Collections.Concurrent;

// Enkel rate limiting per connection: högst N anrop per tidsfönster.
//
// Medvetet enkel för att vara läsbar. I produktion: System.Threading.RateLimiting
// (PartitionedRateLimiter) med nycklar per användare och per IP, inte bara per anslutning,
// eftersom en angripare annars bara öppnar fler anslutningar.
public sealed class InvocationRateLimiter
{
    private readonly ConcurrentDictionary<string, Window> _windows = new();
    private readonly int _limit;
    private readonly TimeSpan _period;

    public InvocationRateLimiter(IConfiguration config)
    {
        _limit = config.GetValue("RateLimit:MaxInvocationsPerWindow", 20);
        _period = TimeSpan.FromSeconds(config.GetValue("RateLimit:WindowSeconds", 10));
    }

    public bool TryAcquire(string connectionId)
    {
        var now = DateTime.UtcNow;

        var window = _windows.AddOrUpdate(
            connectionId,
            _ => new Window(now, 1),
            (_, current) => now - current.Start >= _period
                ? new Window(now, 1)
                : current with { Count = current.Count + 1 });

        return window.Count <= _limit;
    }

    public void Forget(string connectionId) => _windows.TryRemove(connectionId, out _);

    private readonly record struct Window(DateTime Start, int Count);
}
