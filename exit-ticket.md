# Exit ticket

En fristående uppgift för att stämma av vad du har förstått. Gör den gärna vid dagens slut, omkring 15:45, eller när det passar dig. Du behöver inte ha gjort labbarna. Uppgiften lämnas inte in och rättas inte av läraren.

Svara med en till två meningar per fråga. Rätta sedan själv med facit nedan, diskutera och rätta tillsammans med andra, eller be en AI ge återkoppling utifrån frågorna och facit.

## Frågor

1. Förklara med en mening vad SignalR gör som WebSocket inte gör.
2. Varifrån ska servern hämta vem användaren är, och varför inte från metodargumentet?
3. Servern vet att det är Alice som anropar. Räcker det för att låta henne gå med i gruppen Administrators? Varför, eller varför inte?
4. Med `Clients.All` får alla anslutna meddelandet. Ge ett exempel på när det är fel, och vad du väljer i stället.
5. Trafiken är krypterad (wss). Nämn två saker som ändå kan gå fel i vår chatt.
6. Välj en attack från genomgången eller lab 2. Hur kan den stoppas, och var i koden ska skyddet finnas? Om du har gjort lab 3 kan du använda din egen lösning som exempel.

Vad är fortfarande oklart efter idag? Anteckna det du vill repetera eller fråga om.

## Facit för självrättning

Flera formuleringar och exempel kan vara rätt. Jämför resonemanget med svarsförslagen.

1. SignalR är ett ramverk som bland annat ger hub-anrop, grupper och val av transport. WebSocket är en transport för kommunikation åt båda håll.
2. Från den autentiserade identiteten i `Context.User`. Klienten bestämmer metodargumenten och kan ange någon annans namn där.
3. Nej. Autentisering visar vem Alice är. Servern måste också kontrollera hennes behörighet till gruppen, alltså auktorisering.
4. Ett privat meddelande ska inte skickas till alla. Använd exempelvis `Clients.User` för en viss användare, `Clients.Group` för en behörig grupp eller `Clients.Caller` för ett svar till anroparen.
5. Exempel: falskt avsändarnamn, obehörig gruppåtkomst, XSS via `innerHTML`, spam eller meddelanden till fel mottagare. Två exempel räcker. Kryptering skyddar trafiken på vägen men ersätter inte applikationens kontroller.
6. Exempel: stoppa falskt avsändarnamn genom att hämta identiteten från `Context.User` i hubben; stoppa obehörig gruppåtkomst med behörighetskontroller i `JoinGroup` och `SendToGroup`; stoppa XSS genom att rendera med `textContent` i klienten. Identitet och behörighet kontrolleras på servern, medan säker rendering görs där texten visas.

## Om du vill använda AI

Klistra in frågorna, dina svar och facit och be exempelvis:

> Ge återkoppling på mina svar utifrån facit. Förklara vad som stämmer, vad som behöver utvecklas och varför. Godta andra korrekta exempel. Ställ gärna en följdfråga där mitt resonemang är oklart.
