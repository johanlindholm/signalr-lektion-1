using Microsoft.AspNetCore.SignalR;

// TODO 1.2: Gör klassen till en SignalR Hub.
//
// Krav:
//   - Klassen ska ärva från Hub.
//   - En publik metod SendMessage(string username, string message)
//     som skickar meddelandet vidare till ALLA anslutna klienter
//     genom att anropa klientmetoden "ReceiveMessage" med samma två argument.
//
// Ledtrådar:
//   - Clients.All ger dig en proxy mot alla anslutna klienter.
//   - SendAsync("ReceiveMessage", arg1, arg2) ber klienterna köra sin
//     registrerade handler för "ReceiveMessage".
//   - Metoden är asynkron. Returnera Task och använd await.
//
// Fundera redan nu: vad i den här metoden bestämmer klienten, och vad bestämmer servern?
//
// TODO 1.8 (krävs för lab 2 och 3): lägg till gruppfunktionerna.
//   - JoinGroup(string groupName): lägger till anslutningen i gruppen och
//     skickar "ReceiveSystem" med en bekräftelse till anroparen (Clients.Caller).
//   - SendToGroup(string groupName, string username, string message): skickar
//     "ReceiveGroupMessage" med (groupName, username, message) till gruppen (Clients.Group).
//   Ledtrådar: Groups.AddToGroupAsync(Context.ConnectionId, groupName).
//   Facit finns i 02-svag-hub/ChatHub.cs. Skriv dem själv först.

public sealed class ChatHub
{
}
