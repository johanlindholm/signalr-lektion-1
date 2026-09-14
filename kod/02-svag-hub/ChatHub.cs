using Microsoft.AspNetCore.SignalR;

// AVSIKTLIGT SVAG HUB. Kör den aldrig utanför labbmiljön.
//
// Frågor att ställa till varje metod:
//   1. Vilken data kontrolleras av klienten?
//   2. Vad litar servern på utan att kontrollera?
//   3. Vem får informationen?
//   4. Vad händer om anroparen ljuger, spammar eller skickar något orimligt stort?

public sealed class ChatHub : Hub
{
    // Klienten anger själv vem den är.
    public async Task SendMessage(string username, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", username, message);
    }

    // Klienten väljer själv vilken grupp den vill gå med i.
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Caller.SendAsync("ReceiveSystem", $"Du gick med i gruppen {groupName}");
    }

    // Klienten väljer grupp, avsändarnamn och innehåll.
    public async Task SendToGroup(string groupName, string username, string message)
    {
        await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", groupName, username, message);
    }
}
