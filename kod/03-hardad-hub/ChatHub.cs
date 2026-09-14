using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

// Härdad hub. Jämför metod för metod med 02-svag-hub/ChatHub.cs.
//
// Principer som koden följer:
//   - Identitet kommer från Context.User (satt av autentiseringen), aldrig från ett metodargument.
//   - Varje känslig operation kontrolleras när den utförs, inte bara vid anslutning.
//   - Klientens input valideras mot en domänregel (max 500 tecken), inte bara mot en teknisk gräns.
//   - Servern äger regeln om vem som får vara i vilken grupp.
//   - Anrop rate-limitas per connection.
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
        _limiter.Forget(Context.ConnectionId);
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

    private void EnforceRateLimit()
    {
        if (_limiter.TryAcquire(Context.ConnectionId))
        {
            return;
        }

        _logger.LogWarning("Rate limit nådd för {User} ({ConnectionId})", UserName, Context.ConnectionId);
        throw new HubException("För många anrop. Vänta en stund.");
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
