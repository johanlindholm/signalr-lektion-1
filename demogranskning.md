# Demogranskning 15 september 2026

## Resultat

**Kod och instruktioner är genomgångna. 01 och 02 fungerar efter rättningarna. Windows blockerar för närvarande den nybyggda 03-appen, så den fullständiga slutkörningen är inte godkänd.**

| Körning | Utfall | Vad det visar |
|---|---|---|
| Uppdaterade filer före rättning, isolerad kopia | 17 kontroller godkända, startprojektet misslyckades | Chatt, negotiate 200, WebSocket 101, fem attacker, inloggning, identitet, grupper, XSS, validering, storleksgräns, rate limit och båda Broadcast-fallen fungerar. Startprojektet visade felaktigt Ansluten |
| Rättade 01 och 02 | 10 av 10 godkända | Samtliga svaga attacker, nätverkshandskakningen, korrekt TODO-status och återstart utan automatisk reconnect |
| Bygge av alla tre rättade projekten | 0 fel, 0 varningar med SDK 10.0.201 | Koden kompilerar mot net8.0 |
| Fullständig slutkörning | Blockerad vid start av 03 | Windows Application Control nekar ChatHardad.dll med 0x800711C7. Samma fel bekräftat i användarens vanliga terminal |

Webbläsartesterna använde Chromium 145 över HTTPS med betrott utvecklingscertifikat. Certifikatfel ignorerades inte. Testerna körde på separata portar och stoppade bara sina egna serverprocesser.

## Återställda fixar

- Startprojektet visar att TODO:erna återstår. Det visar Ansluten först när anslutningen faktiskt är startad. `window.connection` finns åter för konsolövningarna.
- Attackfilen har separata, körbara varianter för svag och härdad hub. Ett förväntat fel avbryter inte ett senare exempel i samma kodblock. HTTPS-adresser, omladdning efter 1 MB och rate limit-testets 20/10-fördelning är åter dokumenterade.
- SDK, första restore, omstart efter kodändring och Network-fliken är åter tydligt beskrivna. Enbart runtime räcker inte för att bygga.
- Labbarna är frivilliga och jämförs med facit. Exit ticket är fortsatt en separat uppgift med självrättning, gemensam rättning eller AI-stöd. Ingen inlämning eller lärarrättning.

## Behållna uppdateringar

- `Broadcast` med Admin-policy: vanlig användare nekas, admin når alla. Båda fallen verifierades i den första körningen. Ändringen därefter i `ChatHub.cs` gäller endast en förklarande kommentar.
- HTTP/1.1-valet i `Program.cs`: status 101 observerades. Den tidigare motsvarande Development-inställningen behövs därför inte dubbelt.
- Ny karta för lab 3, inloggningsdiagram, HubException-förklaring och serverpush-övning.
- Lärarmaterialets 42 slides och nya fördjupning. En äldre lokal generator spärras från att skriva över det nya appendixet.

## Preciseringar i materialet

Vår Admin-policy kontrollerar rollen. En egen authorization handler kan även läsa metodargument via `HubInvocationContext`; påståendet att inget attribut eller policy kan använda argument var för kategoriskt. Se [Microsofts dokumentation](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-8.0).

Gruppens brutna anslutning tas bort; andra medlemmar försvinner inte. Klientens handler registreras före `JoinCase` så att ett tidigt event inte missas. Identiteten i labbstubben kallas inloggad identitet, eftersom inget lösenord verifieras.

Varningen om `signalr.min.js.map` är en saknad felsökningsfil och påverkar inte chattfunktionen. Biblioteket är oförändrat.

## Återstår i körmiljön

Windows Code Integrity loggar händelse 3077 för `ChatHardad.dll`. Blockeringen gäller även körning utanför sandboxen och kvarstår efter ombygge. Inga skyddsinställningar har ändrats. Ansvarig för datorns App Control-policy behöver hantera tillåtelse/signering innan den nya binären kan startas. Se [Microsofts felsökningsguide](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/app-control-for-business/operations/appcontrol-debugging-and-troubleshooting).

Diffkontrollen visar att Broadcast och flytten av HTTP/1.1-inställningen redan fanns i den testkopia som gick att köra. Mellan den kopian och nuvarande serverkällkod skiljer bara en kommentar i `ChatHub.cs`. Projektfil, beroenden, klientkod och övrig serverlogik är identiska. Båda DLL-filerna är osignerade, men har olika SHA256-hashar. Windows-loggen identifierar policyn `VerifiedAndReputableDesktop` och exakt den nya filens hash. Detta talar för en bedömning av den nybyggda binären; det bevisar inte vilken ändring som utlöste bedömningen. Blockeringen inträffar innan programmets kod körs.

Den utökade testsviten innehåller 23 kontroller. Slutkörningen återstår särskilt för återanslutning med nytt id och återinträde i grupper, privata meddelanden till två anslutningar, formulär/utloggning och faktisk serveromstart av 03. Resultat från den tidigare koden ersätter inte denna slutkörning. Den frivilliga ClockService-snuttens tickning och PowerPoints faktiska rendering har inte körts i denna granskning.

## Kör kontrollerna igen

Från repots rot, med .NET SDK, betrott utvecklingscertifikat samt en installerad Playwright-modul och dess Chromium:

```powershell
dotnet build kod/01-start
dotnet build kod/02-svag-hub
dotnet build kod/03-hardad-hub
node demo-test.cjs
```

Testskriptet använder separata HTTPS-portar 17101–17103, kontrollerar att portarna är lediga och startar redan byggda projekt. Det skriver resultat även om ett test misslyckas. Loggar och skärmbilder sparas under `demo-testresultat/`.

Om Playwright eller Chromium finns på annan plats kan du ange dem uttryckligen:

```text
node demo-test.cjs <playwright-modul> <projektrot> <resultatmapp> <portbas> <chromium.exe>
```

Lägg till `basic` som sista argument för enbart 01 och 02. Kör hela sviten när Windows tillåter 03 igen. De checkade resultaten finns i [testresultat](testresultat/README.md).
