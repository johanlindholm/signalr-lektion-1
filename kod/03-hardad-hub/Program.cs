// 03-hardad-hub
//
// Referenslösning till lab 3. Visar riktningen, inte en färdig produktionslösning.
// Läs kommentarerna "Kvar att göra" längst ner.

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Kestrel kör HTTP/2 över https som standard, och då etableras WebSocket på ett annat sätt
// (RFC 8441, ingen "101 Switching Protocols", Network-fliken visar 200). Vi låser till HTTP/1.1
// så att handshaken från lektionen syns som den är. Ta bort raden i ett riktigt system.
builder.WebHost.ConfigureKestrel(kestrel =>
    kestrel.ConfigureEndpointDefaults(endpoint => endpoint.Protocols = HttpProtocols.Http1));

// 1. Autentisering: vem är du?
//    Cookie-baserad inloggning. Webbläsaren skickar cookien automatiskt med både
//    /negotiate och WebSocket-handshaken, eftersom sidan och hubben ligger på samma origin.
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "chat.auth";
        options.Cookie.HttpOnly = true;                 // JavaScript kommer inte åt cookien
        options.Cookie.SameSite = SameSiteMode.Strict;  // skickas inte från andra sajter
        options.ExpireTimeSpan = TimeSpan.FromHours(8);

        // Ett API ska svara 401/403, inte skicka en 302 till en inloggningssida.
        // Utan detta får SignalR-klienten en HTML-sida tillbaka från /negotiate och felet blir obegripligt.
        options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    });

// 2. Auktorisering: vad får du göra?
//    Policyn används av [Authorize(Policy = "Admin")] på ChatHub.Broadcast. Den kontrolleras per anrop.
//    [Authorize] utan policy, på hubben, kräver bara inloggning och kontrolleras vid anslutning.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
});

// 3. SignalR med explicita gränser.
builder.Services.AddSignalR(options =>
{
    // Standardvärdet är 32 KB. Vi sätter det medvetet och lågt. Ett chattmeddelande behöver inte mer.
    options.MaximumReceiveMessageSize = 4 * 1024;
    // Detaljerade felmeddelanden till klienten bara under utveckling.
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

builder.Services.AddSingleton<GroupPolicy>();
builder.Services.AddSingleton<InvocationRateLimiter>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// Inloggning. STUB: verifierar inga lösenord. I ett riktigt system byts den här endpointen ut mot
// ASP.NET Core Identity eller en extern identitetsleverantör (OpenID Connect). Hubben påverkas inte
// av bytet, eftersom den bara läser Context.User.
app.MapPost("/dev-login", async (HttpContext http, LoginRequest login, IConfiguration config) =>
{
    if (string.IsNullOrWhiteSpace(login.Name) || login.Name.Length > 30 || !login.Name.All(char.IsLetterOrDigit))
    {
        return Results.BadRequest(new { error = "Namnet får bara innehålla bokstäver och siffror, max 30 tecken." });
    }

    var userId = login.Name.ToLowerInvariant();
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, userId),   // används av SignalR för Clients.User(...)
        new(ClaimTypes.Name, login.Name),
    };

    // Servern äger listan över administratörer (appsettings.json), inte klienten.
    var admins = config.GetSection("Auth:AdminUsers").Get<string[]>() ?? [];
    if (admins.Contains(userId, StringComparer.OrdinalIgnoreCase))
    {
        claims.Add(new Claim(ClaimTypes.Role, "Admin"));
    }

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(new ClaimsPrincipal(identity));

    return Results.Ok(new
    {
        name = login.Name,
        roles = claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value),
    });
});

app.MapPost("/logout", async (HttpContext http) =>
{
    await http.SignOutAsync();
    return Results.NoContent();
});

app.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new
{
    name = user.Identity?.Name,
    roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value),
})).RequireAuthorization();

app.MapHub<ChatHub>("/hubs/chat");

app.Run();

// Kvar att göra innan något liknande går i produktion (ämnen för pass 3 och 5):
//   - riktig inloggning med lösenordsverifiering eller extern IdP, och CSRF-skydd för inloggningen
//   - Cookie.SecurePolicy = Always och HTTPS överallt
//   - CORS/Origin-kontroll om klienten ligger på en annan origin
//   - rate limiting per användare och per IP, inte bara per anslutning
//   - gränser för antal samtidiga anslutningar
//   - strukturerad loggning och larm vid nekade anrop
//   - persistens av meddelanden och hantering av reconnect på serversidan

public sealed record LoginRequest(string Name);
