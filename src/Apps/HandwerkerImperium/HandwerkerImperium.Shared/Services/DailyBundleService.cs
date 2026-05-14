using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.Services;

/// <summary>
/// (08.05.2026, Foundation): Daily-Bundle-Rotation.
///
/// Liest die 7 Slots aus <see cref="RemoteConfigKeys.DailyBundleSkus"/> als JSON-Array.
/// Format pro Slot: <c>{"sku": "bundle_x", "title_key": "...", "desc_key": "...",
/// "bonus_screws": 50, "bonus_money": 1000, "speed_hours": 2}</c>
///
/// Rotation: <see cref="GetCurrentBundle"/> berechnet aus dem heutigen UTC-Wochentag,
/// welcher Slot aktiv ist. Beim Wechsel um 00:00 UTC feuert <see cref="BundleRotated"/>.
/// </summary>
public sealed class DailyBundleService : IDailyBundleService, IDisposable
{
    private readonly IRemoteConfigService _remoteConfig;
    private readonly IPurchaseService _purchaseService;
    private readonly IGameStateService _gameStateService;
    private readonly IAnalyticsService? _analyticsService;
    private readonly ILogService _log;

    private DailyBundleOffer[] _slots = new DailyBundleOffer[7];
    private DateTime _lastDayUtc = DateTime.MinValue;
    private readonly object _rotateLock = new(); // Code-Review-Fix [HOCH]: Doppel-Event um 00:00 UTC
    private bool _initialized;
    private bool _disposed;

    public bool IsEnabled { get; private set; }
    public event Action? BundleRotated;

    public DailyBundleService(
        IRemoteConfigService remoteConfig,
        IPurchaseService purchaseService,
        IGameStateService gameStateService,
        ILogService log,
        IAnalyticsService? analyticsService = null)
    {
        _remoteConfig = remoteConfig;
        _purchaseService = purchaseService;
        _gameStateService = gameStateService;
        _log = log;
        _analyticsService = analyticsService;
    }

    public Task InitializeAsync()
    {
        try
        {
            IsEnabled = _remoteConfig.GetBool(RemoteConfigKeys.DailyBundleEnabled, false);
            if (!IsEnabled) return Task.CompletedTask;

            var json = _remoteConfig.GetString(RemoteConfigKeys.DailyBundleSkus, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                _log.Info("DailyBundle: Kein SKU-JSON in RemoteConfig — Bundle deaktiviert.");
                IsEnabled = false;
                return Task.CompletedTask;
            }

            ParseSlots(json);
            _initialized = true;
            _lastDayUtc = DateTime.UtcNow.Date;
        }
        catch (Exception ex)
        {
            _log.Error("DailyBundle-Init fehlgeschlagen", ex);
            IsEnabled = false;
        }
        return Task.CompletedTask;
    }

    public DailyBundleOffer? GetCurrentBundle()
    {
        if (!IsEnabled || !_initialized) return null;

        // Code-Review-Fix [HOCH]: Tageswechsel-Detection im Lock — verhindert dass
        // GameLoop-Tick + ShopVM-UI parallel um 00:00 UTC den Event doppelt feuern.
        var today = DateTime.UtcNow.Date;
        bool rotated = false;
        lock (_rotateLock)
        {
            if (today != _lastDayUtc)
            {
                _lastDayUtc = today;
                rotated = true;
            }
        }
        if (rotated)
        {
            try { BundleRotated?.Invoke(); }
            catch (Exception ex) { _log.Error("BundleRotated-Handler", ex); }
        }

        // Sonntag = 0 in DateTime → wir wollen Mo=0..So=6 (ISO-Standard)
        var dayIdx = ((int)today.DayOfWeek + 6) % 7;
        if (dayIdx < 0 || dayIdx >= _slots.Length) return null;

        var slot = _slots[dayIdx];
        if (slot is null || string.IsNullOrEmpty(slot.Sku)) return null;
        return slot;
    }

    public async Task<bool> PurchaseCurrentBundleAsync()
    {
        var bundle = GetCurrentBundle();
        if (bundle is null) return false;

        _analyticsService?.TrackEvent(AnalyticsEvents.IapPurchaseStarted, new Dictionary<string, object?>
        {
            ["sku"] = bundle.Sku,
            ["bundle_day"] = bundle.DayOfWeekIndex
        });

        var ok = await _purchaseService.PurchaseConsumableAsync(bundle.Sku).ConfigureAwait(false);
        if (!ok)
        {
            _analyticsService?.TrackEvent(AnalyticsEvents.IapPurchaseFailed, new Dictionary<string, object?>
            {
                ["sku"] = bundle.Sku
            });
            return false;
        }

        // Bonus-Items verbuchen — alle Operationen idempotent + lock-frei
        if (bundle.BonusGoldenScrews > 0)
            _gameStateService.AddGoldenScrews(bundle.BonusGoldenScrews, fromPurchase: true);
        if (bundle.BonusMoney > 0)
            _gameStateService.AddMoney(bundle.BonusMoney);
        if (bundle.SpeedBoostHours > 0)
        {
            // Auf bestehenden SpeedBoost stacken
            var current = _gameStateService.State.SpeedBoostEndTime;
            var baseTime = current > DateTime.UtcNow ? current : DateTime.UtcNow;
            _gameStateService.State.SpeedBoostEndTime = baseTime.AddHours(bundle.SpeedBoostHours);
        }

        _analyticsService?.TrackEvent(AnalyticsEvents.IapPurchaseSuccess, new Dictionary<string, object?>
        {
            ["sku"] = bundle.Sku,
            ["bundle_day"] = bundle.DayOfWeekIndex,
            ["bonus_screws"] = bundle.BonusGoldenScrews,
            ["bonus_money"] = (double)bundle.BonusMoney,
            ["speed_hours"] = bundle.SpeedBoostHours
        });
        return true;
    }

    private void ParseSlots(string json)
    {
        // Erlaubt: "{...}" als Wrapper-Objekt mit "slots":[...] ODER direkt "[...]"
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            JsonElement slots = root.ValueKind == JsonValueKind.Array
                ? root
                : root.TryGetProperty("slots", out var arr) ? arr : default;

            if (slots.ValueKind != JsonValueKind.Array) return;

            var nextMidnightUtc = DateTime.UtcNow.Date.AddDays(1);
            int idx = 0;
            foreach (var el in slots.EnumerateArray())
            {
                if (idx >= 7) break;
                _slots[idx] = new DailyBundleOffer
                {
                    DayOfWeekIndex = idx,
                    Sku = el.TryGetProperty("sku", out var sku) ? sku.GetString() ?? string.Empty : string.Empty,
                    TitleKey = el.TryGetProperty("title_key", out var tk) ? tk.GetString() ?? string.Empty : string.Empty,
                    DescriptionKey = el.TryGetProperty("desc_key", out var dk) ? dk.GetString() ?? string.Empty : string.Empty,
                    BonusGoldenScrews = el.TryGetProperty("bonus_screws", out var bs) && bs.TryGetInt32(out var bsI) ? bsI : 0,
                    BonusMoney = el.TryGetProperty("bonus_money", out var bm) && bm.TryGetDecimal(out var bmD) ? bmD : 0m,
                    SpeedBoostHours = el.TryGetProperty("speed_hours", out var sh) && sh.TryGetInt32(out var shI) ? shI : 0,
                    ExpiresAtUtc = nextMidnightUtc,
                };
                idx++;
            }
        }
        catch (Exception ex)
        {
            _log.Error("DailyBundle: SKU-JSON konnte nicht geparst werden", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
