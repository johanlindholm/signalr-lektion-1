# SignalR och säker kommunikation – lektion 1

Bygg en realtidschatt, undersök vad servern litar på och härda sedan din kod. Börja med [labbinstruktionerna](studentmaterial.md).

## Hämta och starta

Klona repot, eller välj **Code → Download ZIP** och packa upp filen.

```bash
git clone https://github.com/johanlindholm/signalr-lektion-1.git
cd signalr-lektion-1
```

Installera .NET SDK 8.0 eller senare och använd exempelvis VS Code eller Visual Studio. Projekten riktar sig mot .NET 8 men tillåter nyare runtime. Enbart runtime räcker inte för att bygga. Gör första restore och bygge med internet före lektionen; referenspaket kan behöva hämtas. JavaScript-klienten ligger lokalt, så npm behövs inte.

Kör från repots rot:

```bash
dotnet --list-sdks
dotnet dev-certs https --trust
dotnet run --project kod/01-start --launch-profile https
```

Öppna **https://localhost:7101**. Låt terminalen vara igång och stoppa med Ctrl+C. Startprojektets sida kan visas, men chatten fungerar först när du fyllt i TODO:erna i lab 1. Öppna sidan via adressen ovan, inte genom att dubbelklicka på HTML-filen.

## Tre steg

| Mapp | Användning |
|---|---|
| [01-start](kod/01-start) | Din arbetskopia. Bygg chatten och arbeta sedan vidare med attacker och härdning. |
| [02-svag-hub](kod/02-svag-hub) | Fungerande, avsiktligt osäker referens och reserv för attackövningen. |
| [03-hardad-hub](kod/03-hardad-hub) | Referens för härdning. Försök själv innan du jämför. |
| [Attacksnuttar](kod/attacker/attacker.md) | Anrop för webbläsarens konsol, med separata varianter för svag och härdad hub. |

De tre stegen hör till samma kursdag. Arbeta i din egen kopia av 01; du behöver inte byta Git-branch mellan labbarna. Gör gärna en commit efter varje labb.

Starta referenserna från repots rot vid behov, i var sin terminal:

```bash
dotnet run --project kod/02-svag-hub --launch-profile https
dotnet run --project kod/03-hardad-hub --launch-profile https
```

02 använder **https://localhost:7102**, 03 använder **https://localhost:7103**. Kör appar och attackövningar lokalt på din egen dator. Koden är undervisningsmaterial, inte en färdig produktionslösning.

I 03 loggar du in som exempelvis `Alice`. Namnet `admin` ger administratörsrollen genom en labbstub utan lösenord. Använd ett vanligt och ett privat webbläsarfönster för olika identiteter, eftersom vanliga flikar delar cookie.

Se [labbinstruktioner och felsökning](studentmaterial.md) samt [mer om projekten](kod/README.md).
