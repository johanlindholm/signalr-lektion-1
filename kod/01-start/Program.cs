// Lab 1: Bygg chatten
//
// Det här projektet är avsiktligt nästan tomt. Din uppgift är att lägga till SignalR
// och en Hub så att två webbläsarflikar kan skicka meddelanden till varandra via servern.
//
// SignalR ingår i ASP.NET Core (Microsoft.AspNetCore.App). Inget NuGet-paket behövs.

using Microsoft.AspNetCore.Server.Kestrel.Core;
var builder = WebApplication.CreateBuilder(args);

// Kestrel kör HTTP/2 över https som standard, och då etableras WebSocket på ett annat sätt
// (RFC 8441, ingen "101 Switching Protocols", Network-fliken visar 200). Vi låser till HTTP/1.1
// så att handshaken från lektionen syns som den är. Ta bort raden i ett riktigt system.
builder.WebHost.ConfigureKestrel(kestrel =>
    kestrel.ConfigureEndpointDefaults(endpoint => endpoint.Protocols = HttpProtocols.Http1));

// TODO 1.1: Registrera SignalR:s tjänster i DI-containern.
//           Ledtråd: en rad, börjar med builder.Services.

var app = builder.Build();

app.UseDefaultFiles();   // Gör att / serverar wwwroot/index.html
app.UseStaticFiles();    // Serverar wwwroot/ (html, js, css)

// TODO 1.3: Mappa din Hub till adressen /hubs/chat.
//           Ledtråd: app.MapHub<...>("...");

app.Run();
