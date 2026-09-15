using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

// Härdad hub. Jämför metod för metod med 02-svag-hub/ChatHub.cs.
//
// Principer som koden följer:
//   - Identitet kommer från Context.User (satt av autentiseringen), aldrig från ett metodargument.
//   - Varje känslig operation kontrolleras när den utförs, inte bara vid anslutning.
//   - Klientens input valideras mot en domänregel (max 500 tecken), inte bara mot en teknisk gräns.
//   - Servern äger regeln om vem som får vara i vilken grupp.
//   - Anrop rate-limitas per användare. Klienten får veta hur länge den ska vänta.
//   - Fel som klienten ska få se kastas som HubException. Allt annat loggas på servern.

[Authorize]
public sealed class ChatHub : Hub<IChatClient>
{
    private const int MaxMessageLength = 500;

    private readonly GroupPolicy _groups;
    private readonly InvocationRateLimiter _limiter;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(GroupPolicy groups, InvocationRateLimiter limiter, ILogger<ChatHub> logger)
    {
        _groups = groups;
        _limiter = limiter;
        _logger = logger;
    }

    // Namnet hämtas från den autentiserade principalen. Klienten kan inte påverka det via Hub-anropet.
    private string UserName => Context.User?.Identity?.Name
        ?? throw new HubException("Användaridentitet saknas.");

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Ansluten: {User} ({ConnectionId})", UserName, Context.ConnectionId);
        await Clients.Caller.ReceiveSystem($"Inloggad som {UserName}");
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        // Räknaren för en connectionId kan tas bort direkt, samma id kommer aldrig tillbaka.
        // Räknaren för en användare tar vi INTE bort här. Annars kunde en spammare nollställa
        // sin gräns genom att koppla ner och ansluta igen. Den städas bort när fönstret löpt ut.
        // Hubben kräver inloggning, så i praktiken är nyckeln alltid användaren.
        if (Context.UserIdentifier is null)
        {
            _limiter.Forget(RateLimitKey);
        }

        _logger.LogInformation("Frånkopplad: {User} ({ConnectionId})", UserName, Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    // Jämför signaturen med den svaga hubben: inget username-argument.
    public async Task SendMessage(string message)
    {
        EnforceRateLimit();
        var text = ValidateMessage(message);

        await Clients.All.ReceiveMessage(UserName, text);
    }

    public async Task JoinGroup(string groupName)
    {
        EnforceRateLimit();

        if (!_groups.MayAccess(Context.User!, groupName))
        {
            _logger.LogWarning("{User} nekades att gå med i gruppen {Group}", UserName, groupName);
            throw new HubException("Du har inte behörighet till den gruppen.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Caller.ReceiveSystem($"Du gick med i gruppen {groupName}");
    }

    public async Task SendToGroup(string groupName, string message)
    {
        EnforceRateLimit();

        // Kontrollen görs igen här. Att en anslutning en gång fick gå med i gruppen är inte ett
        // bevis på att den får skicka nu. Rättigheter kan ha ändrats, och gruppmedlemskap i SignalR
        // är en routingmekanism, inte en säkerhetsmekanism.
        if (!_groups.MayAccess(Context.User!, groupName))
        {
            _logger.LogWarning("{User} nekades att skicka till gruppen {Group}", UserName, groupName);
            throw new HubException("Du har inte behörighet till den gruppen.");
        }

        var text = ValidateMessage(message);
        await Clients.Group(groupName).ReceiveGroupMessage(groupName, UserName, text);
    }

    // Tre nivåer av auktorisering, och det här är nivå två:
    //   1. [Authorize] på klassen: du måste vara inloggad för att ansluta alls. Kontrolleras en gång, vid anslutning.
    //   2. [Authorize(Policy = ...)] på en metod: du måste ha rollen för att anropa just den här metoden.
    //      SignalR kontrollerar det vid varje anrop, innan metoden körs. Utan rollen får klienten ett fel.
    //   3. Egen kod i metoden (GroupPolicy ovan): när beslutet beror på argumentet, till exempel vilken grupp.
    //      Vår Admin-policy kontrollerar bara rollen. En egen authorization handler kan också
    //      läsa argument via HubInvocationContext; här håller vi gruppregeln i GroupPolicy.
    [Authorize(Policy = "Admin")]
    public async Task Broadcast(string message)
    {
        EnforceRateLimit();
        var text = ValidateMessage(message);

        _logger.LogInformation("Broadcast från {User}", UserName);
        await Clients.All.ReceiveSystem($"Meddelande från {UserName}: {text}");
    }

    // Privat meddelande till en användare. Clients.User använder ClaimTypes.NameIdentifier,
    // som sattes vid inloggningen. Mottagaren väljs av klienten, avsändaren av servern.
    public async Task SendPrivate(string toUser, string message)
    {
        EnforceRateLimit();
        var text = ValidateMessage(message);

        if (string.IsNullOrWhiteSpace(toUser) || toUser.Length > 30)
        {
            throw new HubException("Ogiltig mottagare.");
        }

        var recipientId = toUser.ToLowerInvariant();
        await Clients.User(recipientId).ReceivePrivateMessage(UserName, text);
        await Clients.Caller.ReceivePrivateMessage($"{UserName} till {toUser}", text);
    }

    // Vem räknas anropen mot? Här syns valet, och därmed bristen.
    //   - Per anslutning räcker inte: en angripare öppnar bara fler anslutningar och får en ny kvot för varje.
    //   - Per användare (Context.UserIdentifier, från ClaimTypes.NameIdentifier) delas kvoten av alla
    //     användarens flikar och anslutningar. Det är vad vi gör när användaren är inloggad.
    //   - Per IP skulle också begränsa den som skapar många konton. Det har vi inte här.
    // Prefixen gör att ett användarnamn aldrig kan krocka med en connectionId.
    private string RateLimitKey => Context.UserIdentifier is { } userId
        ? $"user:{userId}"
        : $"connection:{Context.ConnectionId}";

    private void EnforceRateLimit()
    {
        var result = _limiter.TryAcquire(RateLimitKey);
        if (result.Allowed)
        {
            return;
        }

        // Avrunda uppåt, så att klienten aldrig försöker för tidigt.
        var seconds = (int)Math.Ceiling(result.RetryAfter.TotalSeconds);

        _logger.LogWarning("Rate limit nådd för {User} ({ConnectionId})", UserName, Context.ConnectionId);
        throw new HubException($"För många anrop. Försök igen om {seconds} sekunder.");
    }

    private static string ValidateMessage(string? message)
    {
        var text = message?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            throw new HubException("Meddelandet får inte vara tomt.");
        }

        if (text.Length > MaxMessageLength)
        {
            throw new HubException($"Meddelandet får vara högst {MaxMessageLength} tecken.");
        }

        return text;
    }
}
