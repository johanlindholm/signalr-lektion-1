# Kod till SignalR-lektionen

Tre ASP.NET Core-projekt och en samling attacksnuttar.

| Mapp | Vad det är | Port https / http |
|---|---|---|
| `01-start` | Startprojekt för studenterna. Hubben är tom med TODO:er | 7101 / 5101 |
| `02-svag-hub` | Fungerande men avsiktligt osäker. Facit till bygget och attackmål | 7102 / 5102 |
| `03-hardad-hub` | Referenslösning med härdning | 7103 / 5103 |
| `attacker/attacker.md` | Konsolsnuttar för attackövningen | |

## Krav

.NET SDK 8.0 eller senare. Projekten är satta att rulla fram till en nyare major (`RollForward=LatestMajor`), så en enbart installerad .NET 10-runtime fungerar också.

Gör restore och bygge med internet före lektionen: en ny dator kan behöva hämta referenspaket för .NET 8. Därefter kan demonstrationen köras offline. SignalR ingår i ASP.NET Core, och JavaScript-klienten ligger lokalt i varje projekt under `wwwroot/lib/signalr.min.js`.

## Kör

```bash
dotnet dev-certs https --trust   # en gång per maskin
cd 02-svag-hub
dotnet run
```

Öppna adressen som skrivs ut, till exempel `https://localhost:7102`. Öppna gärna i två flikar för att se realtidsbeteendet.

## Attackövningen

Kör den svaga hubben, öppna webbläsarens konsol (F12) och använd snuttarna i `attacker/attacker.md`. Anslutningen finns i `window.connection`. Använd sedan varianterna märkta Härdad hub (kräver inloggning först). Dess `SendMessage` tar bara meddelandet, inget användarnamn.

## Inloggning i den härdade hubben

Referenslösningen har en inloggningsstub utan lösenord (`/dev-login`), enbart för labben. Logga in med valfritt användarnamn. Namnet `admin` får rollen Admin och kommer in i gruppen Administrators. Stubben ersätts av riktig autentisering i en senare lektion, och hubben behöver inte ändras när det sker eftersom den bara läser `Context.User`.

## Felsökning

| Symptom | Orsak | Fix |
|---|---|---|
| Certifikatvarning eller direkt anslutningsfel | dev-cert inte betrott | `dotnet dev-certs https --trust`, starta om webbläsaren |
| Porten upptagen | annat projekt kör på samma port | stäng det, eller ändra i `Properties/launchSettings.json` |
| `signalR is not defined` | klientbiblioteket laddas inte | kontrollera `wwwroot/lib/signalr.min.js` och script-ordningen i `index.html` |
| 401 i den härdade hubben | inte inloggad | logga in först via sidan |

Projekten skapades och verifierades mot .NET SDK 8.0. Kontrollera versionslänkarna i lärarhandledningen mot den SDK studenterna faktiskt kör.
