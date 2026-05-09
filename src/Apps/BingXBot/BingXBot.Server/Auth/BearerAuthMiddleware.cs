using BingXBot.Contracts.Api;
using BingXBot.Contracts.Dto;

namespace BingXBot.Server.Auth;

/// <summary>
/// Minimalistische Bearer-Auth: Prueft den Authorization-Header gegen den AuthTokenStore.
/// Oeffentliche Endpoints (/health, /pair/*, /auth/refresh) werden uebersprungen.
///
/// Fuer SignalR-Websocket-Verbindungen: SignalR haengt den Token als `?access_token=...`
/// Query-Parameter an. Dieses Muster unterstuetzt die Middleware ebenfalls.
/// </summary>
public sealed class BearerAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AuthTokenStore _tokens;
    private readonly ILogger<BearerAuthMiddleware> _logger;

    private static readonly string[] PublicPaths =
    {
        ApiRoutes.Health,
        ApiRoutes.PairInit,
        ApiRoutes.PairComplete,
        ApiRoutes.PairCancel,   // Ohne PublicPath: Cancel-Button war auth-required → 401, User konnte
                                 // laufendes Pairing nur per Lifetime-Ablauf (5 min) loswerden.
        ApiRoutes.AuthRefresh,
        // Phase 18 / G4 + H6 — Metrics-Endpoints fuer Prometheus-Scrape + Grafana-JSON-API.
        // Pi-Server steht hinter Tailscale/LAN — Auth waere fuer Scraper aufwendig + bringt
        // wenig Sicherheitsgewinn (Metriken enthalten keine API-Keys, nur Counter/Gauges).
        ApiRoutes.MetricsInternal,
        "/metrics"
    };

    public BearerAuthMiddleware(RequestDelegate next, AuthTokenStore tokens, ILogger<BearerAuthMiddleware> logger)
    {
        _next = next;
        _tokens = tokens;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? string.Empty;
        if (IsPublic(path))
        {
            await _next(ctx);
            return;
        }

        var token = ExtractToken(ctx);
        if (string.IsNullOrEmpty(token) || !_tokens.Validate(token, out var record) || record == null)
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new ErrorResponse("unauthorized", "Token fehlt oder ungueltig"));
            return;
        }

        ctx.Items["AuthenticatedDevice"] = record.DeviceId;
        await _next(ctx);
    }

    private static bool IsPublic(string path)
    {
        // Ordinal (case-sensitive): ASP.NET-Routing ist per Default auch case-sensitive fuer
        // MapGet/MapPost. OrdinalIgnoreCase hier waere inkonsistent — ein Request an /API/V1/HEALTH
        // wuerde die Middleware passieren aber danach im Endpoint-Routing als 404 enden
        // (Info-Leak ob der Pfad existiert + potenzielle Luecke falls spaeter case-insensitive
        // Routing aktiviert wird).
        foreach (var p in PublicPaths)
        {
            if (path.Equals(p, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private static string? ExtractToken(HttpContext ctx)
    {
        var authHeader = ctx.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return authHeader["Bearer ".Length..].Trim();

        // SignalR: access_token als Query-Parameter
        var query = ctx.Request.Query["access_token"].ToString();
        if (!string.IsNullOrWhiteSpace(query)) return query;

        return null;
    }
}
