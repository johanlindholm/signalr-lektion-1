# SignalR och säker kommunikation: labb

Kurs: Datakommunikation och säkerhet
Tid: eftermiddagen, du arbetar själv och läraren finns tillgänglig

Idag bygger du en realtidschatt med SignalR, attackerar den och härdar den. Målet är inte att memorera metoder, utan att träna en vana: för varje steg i flödet, fråga dig vad servern litar på och vilket säkerhetsbeslut som måste fattas där.

Labbarna är frivilliga övningar. Välj själv hur mycket du vill göra och om du arbetar själv eller tillsammans med andra. De lämnas inte in och rättas inte av läraren. Fråga gärna läraren om du kör fast.

Försök gärna själv och jämför sedan med facit: `02-svag-hub` för lab 1, [attacksnuttarna med förväntade resultat](kod/attacker/attacker.md) för lab 2 och `03-hardad-hub` för lab 3. Avsnitten ”Klar när” hjälper dig att kontrollera din lösning. Om du gör alla labbar bygger de vidare på varandra i ordning.

Det här är kursens första pass. Termer som TLS, XSS och spoofing dyker upp idag, men de får sin riktiga genomgång i pass 3 (IT-säkerhet och krypto) och pass 5 (säker kommunikation). Idag räcker det att se vad som händer. Begreppslistan sist i dokumentet har korta förklaringar.

## Dagens röda tråd

Dagens fråga: hur får en server ut information till en klient i samma stund som något händer? Vi går igenom sju steg, och vid varje steg ställer vi samma fråga: vad litar servern på här, och vilket beslut måste den fatta? Det här är svaren, som referens.

| Steg | Vad litar vi på | Beslut |
|---|---|---|
| HTTP | Allt i requesten skrivs av klienten | Kontrollera innan du agerar |
| Polling | Klienten väljer hur ofta den frågar | Tål att någon frågar för ofta |
| SSE | Servern pushar till den som öppnat strömmen | Vem får öppna, och vad får den se |
| WebSocket | Klienten kan skicka vad som helst, när som helst | Kontroll per meddelande, i vår kod |
| SignalR | Ramverket sköter transport och routing | Behörighet är vår sak, inte ramverkets |
| Hub | Argumenten är klientens ord, Context är serverns | Identitet från Context, validera argumenten |
| Säkerhet | Kryptering skyddar vägen, inte besluten | Vem, vad, till vem, hur mycket, hur ofta |

Svaret är nästan alltid detsamma: servern litar på det den själv vet, inte på det klienten säger. Ställ frågan varje gång du skriver en metod som en klient kan anropa.

---

## Gruppövning på förmiddagen: para ihop app och teknik

I par, fyra minuter, under den lärarledda delen. Vilken teknik passar för varje app? Flera svar kan vara rätt. Motivera med de tre frågorna: vem initierar, hur snabbt måste det fram, åt vilket håll går datan.

Teknikerna: request/response, polling, SSE, WebSocket, SignalR.

1. Chatt mellan användare
2. Aktiekurser som tickar på en sida
3. Statuskoll på ett bygge som tar tio minuter
4. Lägg i varukorg och betala
5. AI-chatt som skriver svaret ord för ord
6. Multiplayer-spel eller gemensam whiteboard
7. Notis "du har ett nytt meddelande", några per dag
8. Sensor som rapporterar temperatur var tionde minut

### Referens: fem sätt att få data till klienten, och ett ramverk

| Teknik | Riktning | Styrka | Svaghet | Passar när |
|---|---|---|---|---|
| Request/response | klient frågar, server svarar | enkelt, cachebart, skalar | servern kan inte initiera | data behövs vid dina egna handlingar |
| Polling | klient frågar regelbundet | enkelt, fungerar överallt | fördröjning, onödiga anrop | sekunder till minuter räcker |
| Long polling | klient frågar, servern väntar med svaret | nära realtid över vanlig HTTP | en request per händelse, krångligt på servern | fallback när inget bättre går |
| SSE (Server-Sent Events) | server till klient | enkelt, vanlig HTTP, auto-reconnect | en riktning, text | servern pushar, klienten svarar sällan |
| WebSocket | båda håll | låg latens, binärt | egen hantering av anslutning och reconnect | båda sidor skickar ofta |
| SignalR | båda håll, väljer transport | hub, grupper, reconnect, fallback | .NET-ekosystem, skalning kräver backplane | ASP.NET Core-app som behöver realtid |

SignalR är inte en egen transport. Det är ett ramverk som väljer WebSocket, SSE eller long polling åt dig.

Tre frågor för att välja:

1. Behöver servern kunna skicka utan att klienten frågar? Nej: request/response.
2. Räcker det om datan kommer inom sekunder till minuter? Ja: polling.
3. Skickar klienten också ofta? Nej: SSE. Ja: WebSocket.

Bygger du i ASP.NET Core och vill slippa anslutningshantering själv: SignalR.

---

## Gruppövning på förmiddagen: Hotjakt

Den här gör ni i par under den lärarledda delen. Ingen dator, bara penna och papper. Ni tittar på koden nedan och svarar kort. Sedan tar vi upp svaren gemensamt.

Koden har två sidor. Den övre körs i användarens webbläsare. Den nedre körs på servern. Det klienten skriver i `invoke` landar som argument i hub-metoden med samma namn.

```js
// Klient, körs i webbläsaren (app.js)
await connection.invoke("SendMessage", "Alice", "Hej!");
await connection.invoke("JoinGroup", "Administrators");
```

```csharp
// Server (ChatHub.cs)
public sealed class ChatHub : Hub
{
    public Task SendMessage(string username, string message)
        => Clients.All.SendAsync("ReceiveMessage", username, message);

    public Task JoinGroup(string groupName)
        => Groups.AddToGroupAsync(Context.ConnectionId, groupName);
}
```

(Förkortad. Den fullständiga hubben finns i `kod/02-svag-hub/ChatHub.cs`.)

1. Vilka värden i anropen bestämmer klienten, och vilka sätter servern?
2. Hur skulle du få ett meddelande att se ut som att det kommer från någon annan?
3. Vem får se ett meddelande som skickas med `SendMessage`, och när blir det ett problem?
4. Vad hindrar en klient från att skicka ett meddelande på 1 MB, eller tusen meddelanden i sekunden? Peka i koden.
5. Vad av det här hindras av att trafiken är krypterad (https och wss)?

Spara dina svar. Du känner igen dem i eftermiddagens attackövning.

---

## Innan du börjar

Kör en gång:

```bash
dotnet --version           # 8.0 eller senare
dotnet dev-certs https --trust
```

Det andra kommandot gör utvecklingscertifikatet betrott för `https://localhost`. Gör första restore och bygge med internet före lektionen, eftersom referenspaket kan behöva hämtas. JavaScript-klienten finns redan i projekten.

Projekten ligger i `kod/`:

- `01-start` är din utgångspunkt. Hubben är tom och har TODO:er.
- `02-svag-hub` är en färdig men osäker version. Använd den som facit till lab 1 eller som attackmål.
- `03-hardad-hub` är en referenslösning för härdningen. Försök gärna själv och jämför sedan med den.

Starta från repots rot:

```bash
dotnet run --project kod/01-start --launch-profile https
```

Öppna `https://localhost:7101`. Startprojektet har ännu ingen fungerande chatt. Låt terminalen vara igång. Efter ändringar i C#: stoppa med Ctrl+C och kör samma kommando igen. Efter ändringar i JavaScript: ladda om sidan, gärna med Ctrl+F5.

---

## Lab 1: bygg chatten

Öppna `01-start` i din editor. Fyll i de sju TODO:erna. De ligger i tre filer.

I `Program.cs`:

- TODO 1.1 registrera SignalR i DI-containern
- TODO 1.3 mappa hubben till `/hubs/chat`

I `ChatHub.cs`:

- TODO 1.2 gör klassen till en Hub och skriv `SendMessage(string username, string message)` som skickar vidare till alla anslutna via klientmetoden `ReceiveMessage`

I `wwwroot/app.js`:

- TODO 1.4 skapa anslutningen mot `/hubs/chat`
- TODO 1.5 registrera en handler för `ReceiveMessage`
- TODO 1.6 anropa `SendMessage` när formuläret skickas
- TODO 1.7 starta anslutningen

När chatten fungerar, ett steg till, i både `ChatHub.cs` och `app.js`:

- TODO 1.8 lägg till gruppfunktionerna `JoinGroup` och `SendToGroup` i hubben, och handlerna `ReceiveGroupMessage` och `ReceiveSystem` i klienten. Sidan har inga knappar för grupper, du anropar dem från konsolen. Steget behövs för lab 2 och 3.

Alla ledtrådar finns i filerna.

### Klar när

Du öppnar appen i två flikar, skriver i den ena och meddelandet syns i båda. Öppna sedan Developer Tools (F12), fliken Network, och **ladda om sidan medan Network är öppet**. Hitta:

1. `POST /hubs/chat/negotiate` med filtret All
2. WebSocket-anslutningen (filtrera på WS), statuskod 101
3. de frames som skickas när du skriver ett meddelande (välj WS-raden och Messages)

Stäng den andra fliken och notera att dess anslutning försvinner, som på slide 20.

Kör till sist `connection.invoke("JoinGroup", "General")` i konsolen och se att du får en systemrad tillbaka.

Fundera: var används vanlig HTTP, och var tar den persistenta anslutningen över?

### Om du kör fast

Titta i tabellen sist i det här dokumentet. Kommer du ändå inte vidare, jämför med `02-svag-hub`, som är en fungerande version av precis det här steget.

---

## Lab 2: attackera din egen hub

Nu vänder du verktygen mot din egen kod. Öppna appen i två flikar, öppna konsolen (F12, fliken Console). Anslutningen ligger i `window.connection`. Hann du inte med steg 1.8, kör i stället mot `02-svag-hub`, som har samma hål.

Snuttarna finns i [attacker.md](kod/attacker/attacker.md), numrerade som på slide 27. Kör dem en i taget mot din lokala app. För varje attack, skriv ner tre rader:

```text
Vad jag som angripare skickade eller gjorde
            ↓
Vad servern litade på eller gjorde
            ↓
Vilken konsekvens det fick
```

Attackerna:

1. Skicka ett meddelande som "Administrator". Vem valde avsändarnamnet?
2. Låt den ena fliken gå med i gruppen "Administrators" som legitim admin. Gå sedan med från den andra fliken och skicka till gruppen som "admin". Frågade servern om lov?
3. Skicka `<img src=x onerror=alert('xss')>` som meddelande. Vad händer, och varför?
4. Skicka ett meddelande på 3000 tecken. Skicka sedan ett på 1 MB. Vad är skillnaden i utfall, och varför? (Det stora stänger anslutningen, ladda om sidan efteråt.)
5. Skicka 1000 meddelanden i en loop. Vad stoppar det?

Notera särskilt skillnaden i attack 4. Den ena går rakt igenom, den andra stoppas av en teknisk gräns i SignalR. Vad säger det om skillnaden mellan en teknisk gräns och en verksamhetsregel?

---

## Lab 3: härda hubben

Nu täpper du till hålen du hittade. Gör dem i ordning. Efter varje fix, kör om motsvarande attack och kontrollera att den nu stoppas.

Fortsätt i din egen kod från lab 1, eller i en egen kopia av 02 om du använde reservprojektet. Stegen nedan följer de sju raderna på slide 30.

1. Identitet från servern. Ta bort `username`-argumentet från `SendMessage`. Hämta i stället namnet från serverns kontext. Det förutsätter att användaren är inloggad. Titta i referenslösningen på hur en enkel inloggning sätter upp identiteten.
2. Rätt mottagare. Fråga dig om varje meddelande verkligen ska gå till alla. Lär dig skillnaden mellan `Clients.All`, `Clients.Caller`, `Clients.Others`, `Clients.User` och `Clients.Group`.
3. Grupprättigheter på servern. Låt inte klienten bestämma vilken grupp den får gå med i. Lägg kontrollen på servern.
4. Kontroll vid varje känslig operation. Kontrollera behörigheten både när man går med i en grupp och när man skickar till den. En anslutning lever länge och rättigheter kan ändras.
5. Domänvalidering. Avvisa tomma meddelanden och meddelanden över 500 tecken. Sätt också SignalR:s `MaximumReceiveMessageSize` till `4 * 1024` byte. Visa skillnaden mellan verksamhetsregeln och det tekniska taket.
6. Rate limiting. Hindra obegränsat spam: referensen tillåter 20 anrop per 10 sekunder och anslutning. Det är ett enkelt labbexempel, som på slide 30.
7. Säker rendering. Byt ut osäker DOM-rendering i klienten så att inkommande text visas som text, inte som HTML.

När du tar bort `username` från hubmetoderna måste du också ändra klientens `invoke`-anrop. Använd varianterna märkta Härdad hub i attackfilen. Ett fel om fel antal argument visar inte att längdregeln eller rate limiting fungerar.

I referensen är `SendMessage` en öppen chatt för alla inloggade, så `Clients.All` finns kvar med ett uttalat syfte. Gruppmeddelanden går till `Clients.Group`, och privata meddelanden till `Clients.User`. Jämför mottagarvalen i steg 2.

Inloggningen i 03 är en labbstub utan lösenord: vem som helst kan välja namnet `admin`. Använd Alice i ett vanligt fönster och admin i ett privat fönster för att jämföra behörigheter. Hubben kontrollerar identiteten på anslutningen; exemplet visar inte automatisk uppdatering av roller när en användares rättigheter ändras under en pågående anslutning.

### Klar när

Du kör om minst tre av attackerna från lab 2 mot din härdade hub och visar att de nu stoppas. Du kan förklara, för varje fix, var beslutet fattas och varför det inte kan ligga hos klienten.

Referenslösningen `03-hardad-hub` visar en möjlig väg. Den är inte en färdig produktionslösning, och `Program.cs` i den listar vad som fortfarande saknas.

---

## Stretch, om du blir klar

- Låt servern skicka själv med [ClockService-övningen](fordjupning-clockservice.md), som på slide 35. Frivilligt, med kodförslag och egen kontroll.
- Bygg privata meddelanden med `Clients.User`. Se `SendPrivate` i referenslösningen.
- Logga in som `admin` i referenslösningen och jämför med en vanlig användare. Vem släpps in i gruppen Administrators, och var bestäms det?
- Läs kommentaren längst ner i `Program.cs` i referenslösningen och skriv en egen lista över vad som återstår innan något liknande får gå i produktion.

---

## Exit ticket

[Exit ticket är en egen uppgift med facit](exit-ticket.md). Du kan göra den oberoende av labbarna och rätta själv, tillsammans med andra eller med hjälp av AI. Ingen inlämning eller lärarrättning.

---

## Felsökning

| Symptom | Trolig orsak | Fix |
|---|---|---|
| Certifikatvarning eller anslutning misslyckas direkt | dev-cert inte betrott | `dotnet dev-certs https --trust`, starta om webbläsaren |
| `signalR is not defined` i konsolen | fel sökväg till klientbiblioteket | script-taggen ska peka på `lib/signalr.min.js` och ligga före `app.js` |
| Anslutningen startar men inget syns | glömt registrera `connection.on("ReceiveMessage", ...)` | registrera handlern före `connection.start()` |
| `Failed to invoke SendMessage` | metodnamnet matchar inte hubben | namnen måste vara exakt lika |
| Bara avsändaren ser meddelandet | `Clients.Caller` i stället för `Clients.All` | rätta i hubben |
| 401 efter att du lagt till `[Authorize]` | ingen inloggning gjord | logga in först, se referenslösningens klient |
| `Context.User.Identity.Name` är null | ingen autentisering konfigurerad, eller claim saknas | se `Program.cs` i referensen |

## Begreppslista

| Begrepp | Kort förklaring |
|---|---|
| Realtid | Klienten får uppdateringar nära händelsen utan att ladda om |
| Polling | Klienten frågar servern upprepade gånger efter ny data |
| Long polling | Klienten frågar, servern håller svaret tills något händer, klienten frågar direkt igen |
| SSE | Server-Sent Events. En HTTP-response som hålls öppen så att servern kan skicka händelser. En riktning, server till klient |
| WebSocket | Protokoll för persistent dubbelriktad kommunikation |
| SignalR | ASP.NET Core-ramverk för realtidskommunikation |
| Hub | Nivån där klient och server kan anropa metoder på varandra |
| Negotiation | Inledande utbyte av connection info och transport |
| Transport | Mekanismen som bär kommunikationen, till exempel WebSocket |
| Autentisering | Fastställer vem användaren är |
| Auktorisering | Avgör vad användaren får göra |
| TLS | Kryptering av trafiken mellan klient och server. https är http över TLS, wss är WebSocket över TLS. Mer i pass 3 och 5 |
| wss | WebSocket över TLS, alltså krypterad. ws är samma sak i klartext |
| XSS | När text från en användare körs som kod i en annan användares webbläsare, till exempel via innerHTML. Mer i pass 3 |
| Spoofing | Att utge sig för att vara någon annan, till exempel genom att skicka ett annat användarnamn. Mer i pass 3 |
| Rate limiting | Gräns för hur många anrop en klient får göra per tidsenhet |
| Trust boundary | Gränsen mellan det servern litar på och det den inte litar på. I vår app går den vid hubben |
| Source | Var angriparkontrollerad data kommer in |
| Sink | Där datan används på ett sätt som kan skapa en risk |
