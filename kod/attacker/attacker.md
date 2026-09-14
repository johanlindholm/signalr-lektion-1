# Attacksnuttar för konsolen

Kör dem i webbläsarens konsol (F12, fliken Console) medan du står på chattsidan.
`connection` finns redan i `window` i alla tre apparna.

Numreringen följer studentmaterialets lab 2. Testa varje attack mot **den svaga hubben** (din egen från lab 1, eller `02-svag-hub` på port 5102) och sedan mot **den härdade** (`03-hardad-hub`, port 5103, kräver inloggning). Notera skillnaden i vad servern släpper igenom.

## 1. Spoofing

Skicka ett meddelande som någon annan.

```js
// Svag hub: syns för alla som "Administrator".
await connection.invoke("SendMessage", "Administrator", "Servern startas om nu, logga ut.");
```

```js
// Härdad hub: metoden tar inget username. Servern använder din inloggade identitet.
await connection.invoke("SendMessage", "Administrator", "hej");
// -> Fel: SendMessage tar bara ett argument. Namnet kommer från cookien.
await connection.invoke("SendMessage", "hej alla");
// -> Visas med ditt riktiga namn.
```

## 2. Gå med i en grupp du inte ska ha åtkomst till

Attacken blir bara synlig om någon annan är i gruppen. Öppna två flikar. Den ena spelar administratör, den andra angripare.

```js
// Flik A (spelar admin), svag hub: gå med i gruppen som en legitim admin skulle.
await connection.invoke("JoinGroup", "Administrators");
```

```js
// Flik B (angripare), svag hub: går in i samma grupp utan kontroll och skickar som "admin".
await connection.invoke("JoinGroup", "Administrators");
await connection.invoke("SendToGroup", "Administrators", "admin", "Alla lösenord byts kl 15, skicka ditt nuvarande till mig.");
// -> Flik A ser meddelandet som från "[Administrators] admin".
```

```js
// Härdad hub: servern äger grupprättigheterna.
await connection.invoke("JoinGroup", "Administrators");
// -> Fel: Du har inte behörighet till den gruppen. (om du inte loggade in som admin)
await connection.invoke("JoinGroup", "General");
// -> OK, General är öppen för alla inloggade.
```

## 3. XSS: samma input, olika sink

```js
// Ett meddelande som innehåller HTML och ett skript-liknande fragment.
await connection.invoke("SendMessage", "Eve", "<img src=x onerror=alert('xss')>");
```

Svag hub + svag klient: klienten renderar med `innerHTML`, så bilden laddas, misslyckas och `onerror` kör.
Härdad klient: samma sträng renderas med `textContent` och visas som ofarlig text.

```js
// Härdad hub, inloggad: ett argument.
await connection.invoke("SendMessage", "<img src=x onerror=alert('xss')>");
```

Poängen: risken uppstod inte när datan togs emot, utan när den användes i en farlig sink.

## 4. Stort meddelande (minne och deserialisering)

Kör den lilla varianten först. Den stora stänger anslutningen, och då måste du ladda om sidan innan du kan fortsätta.

```js
// Precis under den tekniska gränsen men långt över en rimlig chattlängd.
await connection.invoke("SendMessage", "Eve", "B".repeat(3000));
// Svag hub: går igenom. Härdad hub: Fel, max 500 tecken.
```

```js
// 1 MB text.
const big = "A".repeat(1024 * 1024);
await connection.invoke("SendMessage", "Eve", big);
```

Svag hub: SignalR:s standardgräns för inkommande meddelanden är 32 KB, så anropet avvisas, och servern stänger anslutningen. Ladda om sidan efteråt.
Det är i sig en poäng: en teknisk defaultgräns fångar det grövsta, men ersätter inte domänvalidering, och 3000 tecken gick ju rakt igenom.

Härdad hub: gränsen är satt till 4 KB, och dessutom finns regeln max 500 tecken.

```js
// Härdad hub: längdregeln avvisar detta. Kör separat från nästa anrop.
await connection.invoke("SendMessage", "B".repeat(3000));
```

```js
// Härdad hub: transportgränsen avvisar detta. Ladda om efteråt.
await connection.invoke("SendMessage", "A".repeat(1024 * 1024));
```

## 5. Spam (rate)

```js
// Många små anrop. 1000 räcker för att se effekten, 10 000 om du vill känna den.
for (let i = 0; i < 1000; i++) {
    connection.invoke("SendMessage", "Eve", "spam " + i);
}
```

Svag hub: inget stoppar det. Alla anslutna klienter översköljs.
Härdad hub: efter ett tjugotal anrop på tio sekunder svarar servern "För många anrop".

```js
// Härdad hub: kör på en ny anslutning. Fånga felen så de blir läsbara.
const resultat = await Promise.allSettled(
    Array.from({ length: 30 }, (_, i) =>
        connection.invoke("SendMessage", "spam " + i))
);
console.table(resultat.map((r, i) => ({
    anrop: i + 1,
    resultat: r.status === "fulfilled" ? "OK" : r.reason.message
})));
```

Använd separata webbläsarprofiler eller ett vanligt och ett privat fönster för Alice och admin i den härdade appen; vanliga flikar delar cookie. Testa även direkt sändning som Alice med `await connection.invoke("SendToGroup", "Administrators", "hej")`: servern ska neka även om du inte först försökt gå med.
