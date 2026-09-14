// 02-svag-hub
//
// Facit till lab 1 och attackmål i lab 2 och i lärarens demo.
// Koden FUNGERAR. Den är också avsiktligt osäker. Läs ChatHub.cs och wwwroot/app.js
// och leta efter allt som klienten bestämmer och servern litar på.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<ChatHub>("/hubs/chat");

app.Run();
