using BingXBot.Contracts.Api;
using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Configuration;
using Microsoft.AspNetCore.RateLimiting;

namespace BingXBot.Server.Api;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Settings, async (ISettingsService settings, CancellationToken ct) =>
            Results.Ok(await settings.GetAsync(ct)));

        app.MapPut(ApiRoutes.Settings, async (HttpContext http, FullSettingsDto dto, ISettingsService settings, CancellationToken ct) =>
        {
            if (!TryValidateRisk(dto.Risk, out var risk)) return Results.BadRequest(new ErrorResponse("invalid_risk", risk));
            if (!TryValidateScanner(dto.Scanner, out var scn)) return Results.BadRequest(new ErrorResponse("invalid_scanner", scn));
            using var _ = BingXBot.Trading.Local.LocalSettingsService.WithSource(GetSource(http));
            await settings.SaveAllAsync(dto, ct);
            return Results.NoContent();
        }).RequireRateLimiting("settings");

        app.MapPut(ApiRoutes.SettingsRisk, async (HttpContext http, RiskSettings dto, ISettingsService settings, CancellationToken ct) =>
        {
            if (!TryValidateRisk(dto, out var reason)) return Results.BadRequest(new ErrorResponse("invalid_risk", reason));
            using var _ = BingXBot.Trading.Local.LocalSettingsService.WithSource(GetSource(http));
            await settings.SaveRiskAsync(dto, ct);
            return Results.NoContent();
        }).RequireRateLimiting("settings");

        app.MapPut(ApiRoutes.SettingsScanner, async (HttpContext http, ScannerSettings dto, ISettingsService settings, CancellationToken ct) =>
        {
            if (!TryValidateScanner(dto, out var reason)) return Results.BadRequest(new ErrorResponse("invalid_scanner", reason));
            using var _ = BingXBot.Trading.Local.LocalSettingsService.WithSource(GetSource(http));
            await settings.SaveScannerAsync(dto, ct);
            return Results.NoContent();
        }).RequireRateLimiting("settings");

        app.MapPut(ApiRoutes.SettingsBot, async (HttpContext http, BotSettings dto, ISettingsService settings, CancellationToken ct) =>
        {
            if (!TryValidateBot(dto, out var reason)) return Results.BadRequest(new ErrorResponse("invalid_bot", reason));
            using var _ = BingXBot.Trading.Local.LocalSettingsService.WithSource(GetSource(http));
            await settings.SaveBotAsync(dto, ct);
            return Results.NoContent();
        }).RequireRateLimiting("settings");

        app.MapPut(ApiRoutes.SettingsBacktest, async (HttpContext http, BacktestSettings dto, ISettingsService settings, CancellationToken ct) =>
        {
            if (!TryValidateBacktest(dto, out var reason)) return Results.BadRequest(new ErrorResponse("invalid_backtest", reason));
            using var _ = BingXBot.Trading.Local.LocalSettingsService.WithSource(GetSource(http));
            await settings.SaveBacktestAsync(dto, ct);
            return Results.NoContent();
        }).RequireRateLimiting("settings");

        // v1.6.3 Phase 14 — Settings-Audit-Trail-History.
        app.MapGet(ApiRoutes.SettingsHistory, async (
            BingXBot.Trading.BotDatabaseService db,
            string? field, DateTime? since, int? limit, CancellationToken ct) =>
        {
            var lim = limit is > 0 ? Math.Min(limit.Value, 1_000) : 200;
            var changes = await db.GetSettingsHistoryAsync(field, since, lim).ConfigureAwait(false);
            var dtos = changes.Select(c => new SettingsChangeDto(
                Timestamp: c.Timestamp,
                Field: c.Field,
                OldValue: c.OldValue,
                NewValue: c.NewValue,
                Source: c.Source,
                Snapshot: c.Snapshot)).ToList();
            return Results.Ok(new SettingsHistoryDto(dtos));
        });
    }

    // ════════════════════════════════════════════════════════════════════════
    // Input-Validation: der Server ist die letzte Instanz gegen fehlerhafte
    // oder malicious Client-Requests. Der Bot soll nicht crashen wenn ein Client
    // MaxLeverage=999, negative Ratios oder NaN schickt.
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// v1.6.3 Phase 14 — Liest "X-BingXBot-Source" Header, normalisiert auf bekannte Werte.
    /// Wenn nicht gesetzt: "User" als Default.
    /// </summary>
    private static string GetSource(HttpContext http)
    {
        if (http.Request.Headers.TryGetValue("X-BingXBot-Source", out var values) && values.Count > 0)
        {
            var v = values[0];
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        }
        return "User";
    }

    private static bool TryValidateRisk(RiskSettings dto, out string reason)
    {
        reason = "";
        if (dto.MaxLeverage < 1 || dto.MaxLeverage > 500) { reason = "MaxLeverage muss zwischen 1 und 500 liegen."; return false; }
        if (dto.MaxPositionSizePercent <= 0 || dto.MaxPositionSizePercent > 100) { reason = "MaxPositionSizePercent muss 0..100 sein."; return false; }
        if (dto.MaxMarginPerTradePercent <= 0 || dto.MaxMarginPerTradePercent > 100) { reason = "MaxMarginPerTradePercent muss 0..100 sein."; return false; }
        if (dto.MaxRiskPercentPerTrade < 0 || dto.MaxRiskPercentPerTrade > 10) { reason = "MaxRiskPercentPerTrade muss 0..10 sein."; return false; }
        if (dto.MaxDailyDrawdownPercent < 0 || dto.MaxDailyDrawdownPercent > 100) { reason = "MaxDailyDrawdownPercent muss 0..100 sein."; return false; }
        if (dto.MaxTotalDrawdownPercent < 0 || dto.MaxTotalDrawdownPercent > 100) { reason = "MaxTotalDrawdownPercent muss 0..100 sein."; return false; }
        if (dto.MaxDailyLossPercent < 0 || dto.MaxDailyLossPercent > 100) { reason = "MaxDailyLossPercent muss 0..100 sein."; return false; }
        if (dto.Tp1CloseRatio < 0.1m || dto.Tp1CloseRatio > 1.0m) { reason = "Tp1CloseRatio muss 0.1..1.0 sein."; return false; }
        if (dto.Tp2CloseRatio < 0m || dto.Tp2CloseRatio > 1.0m) { reason = "Tp2CloseRatio muss 0..1.0 sein."; return false; }
        if (dto.MinRiskRewardRatio < 0m || dto.MinRiskRewardRatio > 20m) { reason = "MinRiskRewardRatio muss 0..20 sein."; return false; }
        if (dto.MaxOpenPositions < 1 || dto.MaxOpenPositions > 100) { reason = "MaxOpenPositions muss 1..100 sein."; return false; }
        if (dto.MaxOpenPositionsPerSymbol < 1 || dto.MaxOpenPositionsPerSymbol > 10) { reason = "MaxOpenPositionsPerSymbol muss 1..10 sein."; return false; }
        return true;
    }

    private static bool TryValidateScanner(ScannerSettings dto, out string reason)
    {
        reason = "";
        if (dto.ActiveTimeframes == null || dto.ActiveTimeframes.Count == 0) { reason = "ActiveTimeframes darf nicht leer sein."; return false; }
#pragma warning disable CS0618 // Legacy-Felder weiterhin validieren bis v1.4-Migration abgeschlossen
        if (dto.MaxResults < 1 || dto.MaxResults > 1000) { reason = "MaxResults muss 1..1000 sein."; return false; }
        if (dto.MinVolume24h < 0) { reason = "MinVolume24h darf nicht negativ sein."; return false; }
#pragma warning restore CS0618
        if (dto.ImpulseAtrMultiplier < 0 || dto.ImpulseAtrMultiplier > 100) { reason = "ImpulseAtrMultiplier muss 0..100 sein."; return false; }
        if (dto.PivotLeftBars < 1 || dto.PivotLeftBars > 50) { reason = "PivotLeftBars muss 1..50 sein."; return false; }
        if (dto.PivotRightBars < 1 || dto.PivotRightBars > 50) { reason = "PivotRightBars muss 1..50 sein."; return false; }
        return true;
    }

    private static bool TryValidateBot(BotSettings dto, out string reason)
    {
        reason = "";
        if (dto.PaperInitialBalance < 0 || dto.PaperInitialBalance > 10_000_000m) { reason = "PaperInitialBalance muss 0..10M sein."; return false; }
        if (dto.SimulatedFundingRatePercent < -1m || dto.SimulatedFundingRatePercent > 1m) { reason = "SimulatedFundingRatePercent muss -1..+1 sein."; return false; }
        return true;
    }

    private static bool TryValidateBacktest(BacktestSettings dto, out string reason)
    {
        reason = "";
        if (dto.InitialBalance < 0 || dto.InitialBalance > 10_000_000m) { reason = "InitialBalance muss 0..10M sein."; return false; }
        return true;
    }
}
