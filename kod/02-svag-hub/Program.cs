// 02-svag-hub
//
// Facit till lab 1 och attackmål i lab 2 och i lärarens demo.
// Koden FUNGERAR. Den är också avsiktligt osäker. Läs ChatHub.cs och wwwroot/app.js
// och leta efter allt som klienten bestämmer och servern litar på.

using Microsoft.AspNetCore.Server.Kestrel.Core;
var builder = WebApplication.CreateBuilder(args);

// Kestrel kör HTTP/2 över https som standard, och då etableras WebSocket på ett annat sätt
// (RFC 8441, ingen "101 Switching Protocols", Network-fliken visar 200). Vi låser till HTTP/1.1
// så att handshaken från lektionen syns som den är. Ta bort raden i ett riktigt system.
builder.WebHost.ConfigureKestrel(kestrel =>
    kestrel.ConfigureEndpointDefaults(endpoint => endpoint.Protocols = HttpProtocols.Http1));

builder.Services.AddSignalR();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<ChatHub>("/hubs/chat");

app.Run();
