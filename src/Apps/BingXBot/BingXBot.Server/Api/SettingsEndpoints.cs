using BingXBot.Contracts.Api;
using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Configuration;
using Microsoft.AspNetCore.Mvc;
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

        // KRITISCH: Die Sub-Settings-DTOs (RiskSettings/ScannerSettings/BotSettings/BacktestSettings)
        // sind alle als DI-Singleton registriert. Ohne [FromBody] entscheidet ASP.NET Minimal-API
        // die Parameter-Quelle ueber den Service-Container — der Endpoint bekam dann die DI-Singleton-
        // Instance (= aktueller Server-State) statt das deserialisierte JSON-Body. Folge: HTTP 204,
        // SettingsChange.Count=0, kein Diff. Mit [FromBody] wird der Request-Body deserialisiert.
        // Bug entdeckt 2026-05-17 via [DBG SaveRiskAsync]-Logs auf dem Pi.

        app.MapPut(ApiRoutes.SettingsRisk, async (HttpContext http, [FromBody] RiskSettings dto, ISettingsService settings, CancellationToken ct) =>
        {
            if (!TryValidateRisk(dto, out var reason)) return Results.BadRequest(new ErrorResponse("invalid_risk", reason));
            using var _ = BingXBot.Trading.Local.LocalSettingsService.WithSource(GetSource(http));
            await settings.SaveRiskAsync(dto, ct);
            return Results.NoContent();
        }).RequireRateLimiting("settings");

        app.MapPut(ApiRoutes.SettingsScanner, async (HttpContext http, [FromBody] ScannerSettings dto, ISettingsService settings, CancellationToken ct) =>
        {
            if (!TryValidateScanner(dto, out var reason)) return Results.BadRequest(new ErrorResponse("invalid_scanner", reason));
            using var _ = BingXBot.Trading.Local.LocalSettingsService.WithSource(GetSource(http));
            await settings.SaveScannerAsync(dto, ct);
            return Results.NoContent();
        }).RequireRateLimiting("settings");

        app.MapPut(ApiRoutes.SettingsBot, async (HttpContext http, [FromBody] BotSettings dto, ISettingsService settings, CancellationToken ct) =>
        {
            if (!TryValidateBot(dto, out var reason)) return Results.BadRequest(new ErrorResponse("invalid_bot", reason));
            using var _ = BingXBot.Trading.Local.LocalSettingsService.WithSource(GetSource(http));
            await settings.SaveBotAsync(dto, ct);
            return Results.NoContent();
        }).RequireRateLimiting("settings");

        app.MapPut(ApiRoutes.SettingsBacktest, async (HttpContext http, [FromBody] BacktestSettings dto, ISettingsService settings, CancellationToken ct) =>
        {
            if (!TryValidateBacktest(dto, out var reason)) return Results.BadRequest(new ErrorResponse("invalid_backtest", reason));
            using var _ = BingXBot.Trading.Local.LocalSettingsService.WithSource(GetSource(http));
            await settings.SaveBacktestAsync(dto, ct);
            return Results.NoContent();
        }).RequireRateLimiting("settings");

        // Cross-Sectional-Momentum-Parameter (Korb-Tuning ohne Code-Deploy). Bewusst NICHT in
        // ISettingsService/FullSettingsDto/Client integriert (haelt den Multi-Client-Sync-Pfad
        // schlank) — dedizierter GET/PUT auf den DI-Singleton, den der CrossSectionalManager per
        // Referenz haelt. Aenderungen wirken beim naechsten Rebalance/Drift-Tick (ein laufender
        // Tick liest die Properties am Anfang). Persistenz ueber BotSettings.CrossSectional.
        app.MapGet(ApiRoutes.SettingsXsec, (CrossSectionalSettings xsec) => Results.Ok(xsec));

        app.MapPut(ApiRoutes.SettingsXsec, async (
            [FromBody] CrossSectionalSettings dto,
            CrossSectionalSettings xsec,
            BotSettings botSettings,
            BingXBot.Trading.BotDatabaseService db,
            CancellationToken ct) =>
        {
            if (!TryValidateXsec(dto, out var reason)) return Results.BadRequest(new ErrorResponse("invalid_xsec", reason));
            // In den DI-Singleton kopieren (Referenz-Identitaet bewahren — der CrossSectionalManager
            // haelt genau diese Instanz).
            xsec.LookbackCandles = dto.LookbackCandles;
            xsec.RebalanceDays = dto.RebalanceDays;
            xsec.LongK = dto.LongK;
            xsec.ShortK = dto.ShortK;
            xsec.RiskAdjusted = dto.RiskAdjusted;
            xsec.LeverageCap = dto.LeverageCap;
            xsec.MarginUtilization = dto.MarginUtilization;
            xsec.AtrStopMultiplier = dto.AtrStopMultiplier;
            xsec.UniverseTopN = dto.UniverseTopN;
            xsec.IncludeTradFi = dto.IncludeTradFi;
            xsec.NavTimeframe = dto.NavTimeframe;
            xsec.CheckIntervalMinutes = dto.CheckIntervalMinutes;
            botSettings.CrossSectional = xsec;
            await db.SaveSettingsAsync(botSettings).ConfigureAwait(false);
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
        if (dto.MaxTotalMarginPercent < 0 || dto.MaxTotalMarginPercent > 100) { reason = "MaxTotalMarginPercent muss 0..100 sein."; return false; }
        if (dto.LossStreakHalveAtCount < 1 || dto.LossStreakHalveAtCount > 50) { reason = "LossStreakHalveAtCount muss 1..50 sein."; return false; }
        if (dto.LossStreakPauseAtCount < 1 || dto.LossStreakPauseAtCount > 50) { reason = "LossStreakPauseAtCount muss 1..50 sein."; return false; }
        if (dto.LossStreakPauseAtCount < dto.LossStreakHalveAtCount) { reason = "LossStreakPauseAtCount muss >= LossStreakHalveAtCount sein."; return false; }
        if (dto.MinPositionSizeRetentionPercent < 0m || dto.MinPositionSizeRetentionPercent > 1m) { reason = "MinPositionSizeRetentionPercent muss 0..1 sein."; return false; }
        if (dto.BreakevenTriggerRMultiple < 0m || dto.BreakevenTriggerRMultiple > 10m) { reason = "BreakevenTriggerRMultiple muss 0..10 sein."; return false; }
        if (dto.MaxNetDirectionalExposurePercent < 0m || dto.MaxNetDirectionalExposurePercent > 1000m) { reason = "MaxNetDirectionalExposurePercent muss 0..1000 sein."; return false; }
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
        if (dto.MaxSlippagePercent < 0m || dto.MaxSlippagePercent > 10m) { reason = "MaxSlippagePercent muss 0..10 sein."; return false; }
        if (dto.MaxSlippagePercentByCategory != null)
        {
            foreach (var kv in dto.MaxSlippagePercentByCategory)
            {
                if (kv.Value < 0m || kv.Value > 10m)
                { reason = $"MaxSlippagePercentByCategory[{kv.Key}] muss 0..10 sein."; return false; }
            }
        }
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

    private static bool TryValidateXsec(CrossSectionalSettings dto, out string reason)
    {
        reason = "";
        if (dto.LookbackCandles < 2 || dto.LookbackCandles > 1000) { reason = "LookbackCandles muss 2..1000 sein."; return false; }
        if (dto.RebalanceDays < 1 || dto.RebalanceDays > 365) { reason = "RebalanceDays muss 1..365 sein."; return false; }
        if (dto.LongK < 0 || dto.LongK > 25) { reason = "LongK muss 0..25 sein."; return false; }
        if (dto.ShortK < 0 || dto.ShortK > 25) { reason = "ShortK muss 0..25 sein."; return false; }
        if (dto.LongK + dto.ShortK < 1) { reason = "LongK + ShortK muss mindestens 1 sein."; return false; }
        if (dto.LeverageCap < 1 || dto.LeverageCap > 20) { reason = "LeverageCap muss 1..20 sein."; return false; }
        if (dto.MarginUtilization <= 0m || dto.MarginUtilization > 1m) { reason = "MarginUtilization muss 0..1 sein."; return false; }
        if (dto.AtrStopMultiplier < 0m || dto.AtrStopMultiplier > 20m) { reason = "AtrStopMultiplier muss 0..20 sein."; return false; }
        if (dto.UniverseTopN < dto.LongK + dto.ShortK || dto.UniverseTopN > 500) { reason = "UniverseTopN muss (LongK+ShortK)..500 sein."; return false; }
        if (dto.CheckIntervalMinutes < 1 || dto.CheckIntervalMinutes > 1440) { reason = "CheckIntervalMinutes muss 1..1440 sein."; return false; }
        if (!Enum.TryParse<BingXBot.Core.Enums.TimeFrame>(dto.NavTimeframe, ignoreCase: true, out _)) { reason = "NavTimeframe ungueltig (z.B. H4, H1, D1)."; return false; }
        return true;
    }
}
