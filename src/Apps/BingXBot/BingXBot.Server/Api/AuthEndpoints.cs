using BingXBot.Contracts.Api;
using BingXBot.Contracts.Dto;
using BingXBot.Server.Auth;
using Microsoft.AspNetCore.RateLimiting;

namespace BingXBot.Server.Api;

public static class AuthEndpoints
{
    // Prozessstart-Zeit fuer /health uptimeSeconds. Der explizite static ctor entfernt beforefieldinit,
    // damit ProcessStart beim ersten Klassenzugriff (MapAuthEndpoints im Startup) gesetzt wird — sonst
    // initialisierte das Feld erst beim ERSTEN /health-Hit (uptime startete bei 0 statt beim Prozessstart).
    private static readonly DateTime ProcessStart;
    static AuthEndpoints() => ProcessStart = DateTime.UtcNow;

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Health, () =>
        {
            // Keine Server-Version exposed (Info-Disclosure). SchemaVersion reicht fuer Client-Kompatibilitaet.
            var uptime = (long)(DateTime.UtcNow - ProcessStart).TotalSeconds;
            return Results.Ok(new HealthResponse(
                Status: "ok",
                SchemaVersion: 1,
                UptimeSeconds: uptime));
        });

        app.MapPost(ApiRoutes.PairInit, (PairInitRequest request, PairingService pairing) =>
        {
            if (string.IsNullOrWhiteSpace(request.DeviceName))
                return Results.BadRequest(new ErrorResponse("invalid_request", "DeviceName fehlt"));

            try
            {
                var (pairingId, lifetime) = pairing.StartPairing(request.DeviceName);
                return Results.Ok(new PairInitResponse(
                    PairingId: pairingId,
                    CodeLengthDigits: 6,
                    ExpiresInSeconds: lifetime));
            }
            catch (InvalidOperationException ex)
            {
                // Spam-Schutz (bereits aktiver Pair-Vorgang)
                return Results.Conflict(new ErrorResponse("pairing_busy", ex.Message));
            }
        }).RequireRateLimiting("pair-init");

        app.MapPost(ApiRoutes.PairComplete, (PairCompleteRequest request, PairingService pairing, AuthTokenStore tokens) =>
        {
            if (string.IsNullOrWhiteSpace(request.PairingId)
                || string.IsNullOrWhiteSpace(request.Code)
                || string.IsNullOrWhiteSpace(request.DeviceId))
                return Results.BadRequest(new ErrorResponse("invalid_request", "PairingId/Code/DeviceId fehlt"));

            var outcome = pairing.TryComplete(request.PairingId, request.Code, out var deviceName);
            return outcome switch
            {
                PairCompleteOutcome.Success => Results.Ok(IssueResponse(tokens, request.DeviceId, deviceName)),

                // Tippfehler: Session lebt, Client soll den User neu tippen lassen.
                PairCompleteOutcome.InvalidCode => Results.Json(
                    new ErrorResponse("invalid_code", "Code falsch. Bitte erneut eingeben."),
                    statusCode: StatusCodes.Status401Unauthorized),

                // Session tot: Client muss zurück in Schritt 1 (PairingId clearen, 'Pairing starten' erneut klicken).
                PairCompleteOutcome.TooManyAttempts => Results.Json(
                    new ErrorResponse("pairing_exhausted",
                        "Zu viele Fehlversuche — Pairing wurde abgebrochen. Bitte neu starten."),
                    statusCode: StatusCodes.Status401Unauthorized),

                PairCompleteOutcome.Expired => Results.Json(
                    new ErrorResponse("pairing_expired",
                        "Pairing-Code abgelaufen. Bitte neu starten."),
                    statusCode: StatusCodes.Status401Unauthorized),

                PairCompleteOutcome.UnknownPairingId => Results.Json(
                    new ErrorResponse("pairing_unknown",
                        "Pairing-Sitzung unbekannt (Server-Neustart?). Bitte neu starten."),
                    statusCode: StatusCodes.Status401Unauthorized),

                _ => Results.Json(
                    new ErrorResponse("invalid_code", "Pairing fehlgeschlagen."),
                    statusCode: StatusCodes.Status401Unauthorized)
            };

            static PairCompleteResponse IssueResponse(AuthTokenStore tokens, string deviceId, string? deviceName)
            {
                var rec = tokens.IssueToken(deviceId, deviceName ?? "Unknown");
                return new PairCompleteResponse(
                    Token: rec.Token,
                    RefreshToken: rec.RefreshToken,
                    TokenLifetimeSeconds: tokens.TokenLifetimeSeconds,
                    SchemaVersion: 1);
            }
        }).RequireRateLimiting("pair-complete");

        // Pair-Cancel: Loescht aktives Pending-Pair (fuer Abbruch-Dialog in der App).
        // Oeffentlicher Endpoint — braucht keine Token-Auth weil vor Complete passiert.
        // Rate-Limit via "pair-init"-Bucket (gleiche Quelle, gleiche Spam-Flaeche).
        app.MapPost(ApiRoutes.PairCancel, (PairCancelRequest request, PairingService pairing) =>
        {
            if (string.IsNullOrWhiteSpace(request.PairingId))
                return Results.BadRequest(new ErrorResponse("invalid_request", "PairingId fehlt"));
            var cancelled = pairing.CancelPairing(request.PairingId);
            return cancelled ? Results.NoContent() : Results.NotFound(new ErrorResponse("not_found"));
        }).RequireRateLimiting("pair-init");

        app.MapPost(ApiRoutes.AuthRefresh, (TokenRefreshRequest request, AuthTokenStore tokens) =>
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
                return Results.BadRequest(new ErrorResponse("invalid_request", "RefreshToken fehlt"));

            var rec = tokens.Refresh(request.RefreshToken);
            if (rec == null)
                return Results.Json(new ErrorResponse("invalid_refresh_token"),
                    statusCode: StatusCodes.Status401Unauthorized);

            return Results.Ok(new TokenRefreshResponse(
                Token: rec.Token,
                RefreshToken: rec.RefreshToken,
                TokenLifetimeSeconds: tokens.TokenLifetimeSeconds));
        }).RequireRateLimiting("refresh");

        // Phase 18 / G3 — Single-Device-Logout: revoked nur den Bearer-Token des aktuellen Aufrufs.
        // Der RefreshToken bleibt im Store (Tap kann erneut refreshed werden), allerdings ohne
        // gueltigen Access-Token muss der Client trotzdem refreshen — d.h. de facto Single-Logout.
        // Fuer "alle Tokens raus" siehe AuthLogoutOthers + zusaetzlichem expliziten Refresh-Revoke.
        app.MapPost(ApiRoutes.AuthLogout, (HttpContext ctx, AuthTokenStore tokens) =>
        {
            var current = ExtractBearerToken(ctx);
            if (string.IsNullOrEmpty(current))
                return Results.Json(new ErrorResponse("invalid_request", "Kein Bearer-Token im Header"),
                    statusCode: StatusCodes.Status401Unauthorized);
            tokens.Revoke(current);
            return Results.NoContent();
        });

        // Phase 18 / G3 — Logout-überall: revoked alle Tokens AUSSER dem aktuell genutzten.
        // Use-case: "Mein Handy ist weg, ich melde mich vom Desktop und schmeisse alle anderen Sessions raus".
        app.MapPost(ApiRoutes.AuthLogoutOthers, (HttpContext ctx, AuthTokenStore tokens) =>
        {
            var current = ExtractBearerToken(ctx);
            if (string.IsNullOrEmpty(current))
                return Results.Json(new ErrorResponse("invalid_request", "Kein Bearer-Token im Header"),
                    statusCode: StatusCodes.Status401Unauthorized);
            var removed = tokens.RevokeAllExcept(current);
            return Results.Ok(new { revokedCount = removed });
        });
    }

    /// <summary>Phase 18 / G3 — Helper: extrahiert den Bearer-Token aus dem Authorization-Header.</summary>
    private static string? ExtractBearerToken(HttpContext ctx)
    {
        var auth = ctx.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(auth)) return null;
        const string prefix = "Bearer ";
        if (auth.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return auth.Substring(prefix.Length).Trim();
        return null;
    }
}
