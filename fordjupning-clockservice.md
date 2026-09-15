# Fördjupning: låt servern skicka själv

En frivillig fortsättning på slide 37. Ingen inlämning eller lärarrättning. Försök gärna själv och jämför med kodförslaget nedan.

## Uppgift

Låt servern skicka klockslaget till alla anslutna klienter var tionde sekund, utan att någon klient anropar en hub-metod. Använd en `BackgroundService` och injicera `IHubContext<ChatHub>`.

Arbeta i din egen lösning från lab 1 eller en egen kopia av `kod/02-svag-hub`. Exemplet förutsätter en otypad `ChatHub : Hub` och en klient som lyssnar på `ReceiveSystem`, som i 02.

## Kodförslag

Skapa `ClockService.cs` bredvid `Program.cs`:

```csharp
using Microsoft.AspNetCore.SignalR;

public sealed class ClockService(IHubContext<ChatHub> hub) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await hub.Clients.All.SendAsync(
                "ReceiveSystem", $"Klockan är {DateTime.Now:HH:mm:ss}", ct);
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
        }
    }
}
```

Registrera tjänsten i `Program.cs` före `builder.Build()`:

```csharp
builder.Services.AddHostedService<ClockService>();
```

Klienten i 02 har redan en `connection.on("ReceiveSystem", ...)` som visar meddelandet. Om du arbetar i 01 behöver du ha lagt till motsvarande handler.

Arbetar du i 03, som använder `Hub<IChatClient>`, kan du i stället injicera `IHubContext<ChatHub, IChatClient>` och skicka med `await hub.Clients.All.ReceiveSystem(...)`.

## Kontrollera själv

1. Starta appen och öppna två flikar. Vänta på klockslag i båda utan att skicka något från klienterna.
2. Stäng en flik. Den andra ska fortsätta få klockslag.
3. Jämför `Clients.All` med `Clients.Group("General")`. Vilka får meddelandet då? Gå med i gruppen från en flik för att prova.

Fundera: varför finns ingen `Caller` i `IHubContext`? Vem skulle få utlösa ett utskick om en HTTP-endpoint ersatte klockan?

Svarsstöd: bakgrundstjänsten kör utan ett hub-anrop, så det finns ingen anropande anslutning. `All` når alla anslutna klienter i det här enserversexemplet; `Group` når gruppens anslutningar. En endpoint behöver egna regler för vem som får utlösa utskicket och vad som får skickas. Hubben skyddar inte automatiskt andra endpoints.

Se [Microsofts beskrivning av IHubContext](https://learn.microsoft.com/en-us/aspnet/core/signalr/hubcontext?view=aspnetcore-8.0).
