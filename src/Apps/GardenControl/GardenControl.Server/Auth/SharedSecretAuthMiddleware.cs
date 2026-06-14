using System.Security.Cryptography;
using System.Text;
using GardenControl.Core;

namespace GardenControl.Server.Auth;

/// <summary>
/// Shared-Secret-Header-Auth (analog BingXBot-Server, aber ohne Pairing/Token-Rotation).
/// Prueft bei ALLEN /api-Endpunkten UND am SignalR-Hub (/hub/garden) den Header
/// <see cref="GardenAuth.SecretHeader"/> gegen das konfigurierte Server-Secret.
///
/// Ohne diesen Schutz konnte jedes LAN-Geraet und jede im Browser geoeffnete Webseite
/// (CSRF/DNS-Rebinding) ungeschuetzt Hardware schalten (Ventile/Pumpe/Notstopp/Modus).
///
/// Oeffentlich (kein Secret noetig): nur der Health-/Ping-Endpoint (<c>/api/health</c>) —
/// damit der Client "Server erreichbar?" von "Secret falsch?" unterscheiden kann.
///
/// SignalR-WebSocket: Header sind beim WebSocket-Upgrade nicht immer setzbar, daher akzeptiert
/// die Middleware das Secret am Hub-Pfad zusaetzlich als Query-Parameter <c>?access_token=...</c>
/// (dasselbe Muster wie der BingXBot-Server).
/// </summary>
public sealed class SharedSecretAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SharedSecretAuthMiddleware> _logger;
    private readonly byte[] _expectedSecretBytes;

    // Health-/Ping-Endpoint bleibt offen (leakt keinen Zustand, nur "ok").
    private const string PublicHealthPath = "/api/health";

    // Hub-Pfad: hier ist das Secret zusaetzlich als ?access_token=... erlaubt (WebSocket-Upgrade).
    private const string HubPath = "/hub/garden";

    public SharedSecretAuthMiddleware(RequestDelegate next, IConfiguration config, ILogger<SharedSecretAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;

        var secret = config["Auth:SharedSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            // Kein Secret konfiguriert → Default-Dev-Secret. Auf dem Pi MUSS Auth__SharedSecret
            // gesetzt werden; ansonsten warnen wir laut, damit der Produktivbetrieb nicht
            // versehentlich mit dem oeffentlich bekannten Default-Secret laeuft.
            secret = GardenAuth.DefaultDevSecret;
            _logger.LogWarning(
                "Kein Auth:SharedSecret konfiguriert — verwende Default-Dev-Secret. " +
                "Fuer den Pi-Betrieb Auth__SharedSecret als Umgebungsvariable setzen!");
        }

        _expectedSecretBytes = Encoding.UTF8.GetBytes(secret);
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? string.Empty;

        // Health bleibt offen.
        if (path.Equals(PublicHealthPath, StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        var presented = ExtractSecret(ctx, path);
        if (presented == null || !IsValid(presented))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsJsonAsync(new { error = "unauthorized", message = "Secret fehlt oder ungueltig" });
            return;
        }

        await _next(ctx);
    }

    private static string? ExtractSecret(HttpContext ctx, string path)
    {
        // Primaer: Header (REST + SignalR-Long-Polling/SSE setzen ihn problemlos).
        var header = ctx.Request.Headers[GardenAuth.SecretHeader].ToString();
        if (!string.IsNullOrWhiteSpace(header))
            return header;

        // SignalR-WebSocket-Upgrade: Secret als access_token-Query (nur am Hub-Pfad).
        if (path.StartsWith(HubPath, StringComparison.OrdinalIgnoreCase))
        {
            var query = ctx.Request.Query["access_token"].ToString();
            if (!string.IsNullOrWhiteSpace(query))
                return query;
        }

        return null;
    }

    private bool IsValid(string presented)
    {
        // Konstante-Zeit-Vergleich gegen Timing-Angriffe (FixedTimeEquals vergleicht
        // erst nach Laengen-Pruefung, ist aber bei Laengen-Mismatch trotzdem konstant je Laenge).
        var presentedBytes = Encoding.UTF8.GetBytes(presented);
        return CryptographicOperations.FixedTimeEquals(presentedBytes, _expectedSecretBytes);
    }
}
