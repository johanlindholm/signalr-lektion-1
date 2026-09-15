using System.Diagnostics;

/// <summary>
/// Enkel rate limiting: högst N anrop per tidsfönster för varje nyckel.
/// </summary>
/// <remarks>
/// MÅSTE registreras som singleton (se Program.cs). Räknarna ligger i ett fält i objektet.
/// Registreras klassen som scoped eller transient får varje hubbanrop ett nytt, tomt objekt,
/// och då begränsar den ingenting, utan att något fel syns.
/// </remarks>
//
// Medvetet enkel för att vara läsbar. Kända begränsningar:
//   - Fixed window: räknaren nollställs när fönstret byts. Därför kan en klient göra 20 anrop i slutet
//     av ett fönster och 20 i början av nästa, alltså 40 på kort tid. Sliding window och token bucket
//     jämnar ut det.
//   - Räknarna finns i minnet i en process. Med flera serverinstanser (flera pods, Azure SignalR
//     med flera appservrar) har varje instans egna räknare, och gränsen blir i praktiken N × limit.
//     Då behövs en delad lagring, till exempel Redis.
//   - I produktion: System.Threading.RateLimiting (PartitionedRateLimiter), som har färdiga
//     fixed window, sliding window och token bucket.
public sealed class InvocationRateLimiter
{
    private readonly int _maxInvocations;
    private readonly TimeSpan _windowLength;

    // Nyckel -> fönstret för den nyckeln. En vanlig Dictionary är inte trådsäker, och SignalR kan
    // köra flera hubbanrop samtidigt på olika trådar. Därför går all åtkomst genom samma lås.
    private readonly Dictionary<string, Window> _windows = new();
    private readonly object _lock = new();
    private long _lastCleanup = Stopwatch.GetTimestamp();

    public InvocationRateLimiter(IConfiguration config)
    {
        _maxInvocations = config.GetValue("RateLimit:MaxInvocationsPerWindow", 20);
        var windowSeconds = config.GetValue("RateLimit:WindowSeconds", 10);

        // Fel konfiguration ska stoppa starten, inte tyst stänga av skyddet.
        // 0 i MaxInvocationsPerWindow skulle blockera allt, 0 i WindowSeconds skulle släppa igenom allt.
        if (_maxInvocations < 1)
        {
            throw new InvalidOperationException("RateLimit:MaxInvocationsPerWindow måste vara minst 1.");
        }

        if (windowSeconds < 1)
        {
            throw new InvalidOperationException("RateLimit:WindowSeconds måste vara minst 1.");
        }

        _windowLength = TimeSpan.FromSeconds(windowSeconds);
    }

    // partitionKey avgör vem anropen räknas mot. Hubben väljer nyckeln, se ChatHub.RateLimitKey.
    public RateLimitResult TryAcquire(string partitionKey)
    {
        // Stopwatch.GetTimestamp() i stället för DateTime.UtcNow. Klockan på datorn kan ställas om
        // (NTP, sommartid, en administratör), och går den bakåt tar fönstret aldrig slut.
        // Stopwatch räknar bara framåt. Samma regel gäller för cachning, timeouts och tokenlivstider:
        // mät tidsskillnader med en monoton klocka.
        var now = Stopwatch.GetTimestamp();

        lock (_lock)
        {
            RemoveExpiredWindows(now);

            if (!_windows.TryGetValue(partitionKey, out var window) || IsExpired(window, now))
            {
                window = new Window { StartedAt = now };
                _windows[partitionKey] = window;
            }

            if (window.Count >= _maxInvocations)
            {
                var retryAfter = _windowLength - Stopwatch.GetElapsedTime(window.StartedAt, now);
                return new RateLimitResult(false, retryAfter);
            }

            window.Count++;
            return new RateLimitResult(true, TimeSpan.Zero);
        }
    }

    // Frivillig städning när en nyckel vet att den inte kommer tillbaka, till exempel en connectionId
    // efter disconnect. Minnet skyddas ändå av RemoveExpiredWindows, även om Forget aldrig anropas.
    public void Forget(string partitionKey)
    {
        lock (_lock)
        {
            _windows.Remove(partitionKey);
        }
    }

    // Utan städning växer dictionaryn för varje ny nyckel. En angripare som öppnar anslutningar i
    // en loop skulle då styra hur mycket minne servern använder. En gång per fönsterlängd tar vi bort
    // fönster som redan löpt ut. Anropas inuti låset.
    private void RemoveExpiredWindows(long now)
    {
        if (Stopwatch.GetElapsedTime(_lastCleanup, now) < _windowLength)
        {
            return;
        }

        var expiredKeys = _windows
            .Where(pair => IsExpired(pair.Value, now))
            .Select(pair => pair.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _windows.Remove(key);
        }

        _lastCleanup = now;
    }

    private bool IsExpired(Window window, long now) =>
        Stopwatch.GetElapsedTime(window.StartedAt, now) >= _windowLength;

    private sealed class Window
    {
        public long StartedAt { get; init; }   // tidsstämpel från Stopwatch.GetTimestamp()
        public int Count { get; set; }
    }
}

// Svaret bär med sig hur länge klienten ska vänta. En bool säger bara nej, inte när det blir ja igen.
public sealed record RateLimitResult(bool Allowed, TimeSpan RetryAfter);
