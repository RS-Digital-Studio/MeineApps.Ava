using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Implementierung des Workshop-Rebirth-Systems.
/// Bei Level 1000 kann ein Workshop wiedergeboren werden: Level zurück auf 1, dafür +1 permanenter Stern.
/// Sterne geben Einkommens-Bonus, Upgrade-Rabatt und Extra-Worker-Slots.
/// Sterne überleben Prestige + Ascension (permanentester Fortschritt im Spiel).
/// </summary>
public sealed class RebirthService : IRebirthService
{
    private readonly IGameStateService _gameStateService;
    private readonly IAudioService _audioService;
    private readonly IPrestigeService _prestigeService;
    private readonly IAscensionService _ascensionService;

    /// <summary>
    /// Kosten-Tabelle pro nächstem Stern: (Goldschrauben, Geld-Prozent).
    /// Stern 1 = günstig (Einstieg), Stern 5 = sehr teuer (Endgame-Ziel).
    ///
    /// Fix 18.04.2026 Game-Audit: Gesamt-GS halbiert von 2350 auf 1175 pro Workshop
    /// (10 Workshops: 23.500 → 11.750 GS). Mit best-case ~110 GS/Tag F2P: 210 Tage → 107 Tage.
    ///
    /// v2.0.37 (05.05.2026): Stern 4+5 weiter reduziert (250→200, 500→400). Senkt
    /// pro Workshop nochmal 150 GS (1500 GS bei 10 Workshops, ~14 Tage F2P).
    /// Premium-Wert bleibt erhalten (Premium-Pass halbiert ohnehin nochmal).
    /// </summary>
    private static readonly (int goldenScrews, decimal moneyPercent)[] RebirthCosts =
    [
        ( 50, 0.10m),   // Stern 1 (von 100)
        (125, 0.15m),   // Stern 2 (von 250)
        (250, 0.20m),   // Stern 3 (von 500)
        (200, 0.25m),   // Stern 4 (v2.0.37: 250 → 200)
        (400, 0.30m),   // Stern 5 (v2.0.37: 500 → 400)
    ];

    public event EventHandler<WorkshopType>? RebirthCompleted;

    private readonly IAnalyticsService? _analyticsService;

    public RebirthService(
        IGameStateService gameStateService,
        IAudioService audioService,
        IPrestigeService prestigeService,
        IAscensionService ascensionService,
        IAnalyticsService? analyticsService = null)
    {
        _gameStateService = gameStateService;
        _audioService = audioService;
        _prestigeService = prestigeService;
        _ascensionService = ascensionService;
        _analyticsService = analyticsService;

        // Nach State-Load Sterne auf Workshop-Instanzen übertragen
        _gameStateService.StateLoaded += (_, _) => ApplyStarsToWorkshops();

        // Nach Prestige/Ascension: Neue Workshops erhalten Sterne
        // (StateLoaded feuert NICHT bei Prestige/Ascension, nur bei Load/Import/Reset)
        _prestigeService.PrestigeCompleted += (_, _) => ApplyStarsToWorkshops();
        _ascensionService.AscensionCompleted += (_, _) => ApplyStarsToWorkshops();
    }

    public bool CanRebirth(WorkshopType type)
    {
        var workshop = _gameStateService.State.Workshops.FirstOrDefault(w => w.Type == type);
        if (workshop == null) return false;

        return workshop.Level >= Workshop.MaxLevel && GetStars(type) < 5;
    }

    public (int goldenScrews, decimal moneyPercent) GetRebirthCost(WorkshopType type)
    {
        int nextStar = GetStars(type) + 1;

        // Index 0-basiert: Stern 1 = Index 0, Stern 5 = Index 4
        if (nextStar >= 1 && nextStar <= RebirthCosts.Length)
            return RebirthCosts[nextStar - 1];

        // Fallback: sollte nie passieren (CanRebirth prüft < 5)
        return (int.MaxValue, 1.0m);
    }

    public bool DoRebirth(WorkshopType type)
    {
        if (!CanRebirth(type))
            return false;

        var state = _gameStateService.State;
        var workshop = state.Workshops.FirstOrDefault(w => w.Type == type);
        if (workshop == null)
            return false;

        var cost = GetRebirthCost(type);

        // Geldkosten VOR der Goldschrauben-Buchung berechnen
        // Falls GameLoop zwischenzeitlich Money senkt, wird TrySpendMoney unten sauber ablehnen
        decimal moneyCost = state.Money * cost.moneyPercent;

        // Goldschrauben prüfen und abziehen
        if (!_gameStateService.TrySpendGoldenScrews(cost.goldenScrews))
            return false;

        // Geld atomar abziehen (gegen Race mit GameLoop-Events wie TaxAudit/WorkerStrike)
        // Bei Fehlschlag: Goldschrauben zurueckgeben, damit Spieler nichts verliert.
        // fromPurchase:true verhindert Premium/Prestige-GS-Boni auf den Rollback-Betrag
        // (sonst bekaeme Premium-Spieler mit Prestige-Shop bis zu 2.5x der gezahlten GS zurueck → Exploit).
        if (!_gameStateService.TrySpendMoney(moneyCost))
        {
            _gameStateService.AddGoldenScrews(cost.goldenScrews, fromPurchase: true);
            return false;
        }

        // Stern erhöhen
        int newStars = GetStars(type) + 1;
        state.WorkshopStars[type.ToString()] = newStars;

        // Workshop Level auf 1 zurücksetzen
        // TotalEarned und Worker bleiben erhalten (bewusst kein Reset)
        workshop.Level = 1;

        // Sterne sofort auf alle Workshop-Instanzen anwenden
        ApplyStarsToWorkshops();

        // Einkommens-Cache invalidieren (neuer Stern ändert Boni)
        state.InvalidateIncomeCache();


        // Audio-Feedback (LevelUp-Sound passt für Rebirth-Feier)
        _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();

        RebirthCompleted?.Invoke(this, type);

        _analyticsService?.TrackEvent(AnalyticsEvents.RebirthDone, new Dictionary<string, object?>
        {
            ["workshop"] = type.ToString(),
            ["star_level"] = GetStars(type)
        });

        return true;
    }

    public int GetStars(WorkshopType type)
    {
        return _gameStateService.State.WorkshopStars.GetValueOrDefault(type.ToString(), 0);
    }

    public void ApplyStarsToWorkshops()
    {
        var state = _gameStateService.State;
        for (int i = 0; i < state.Workshops.Count; i++)
        {
            var workshop = state.Workshops[i];
            workshop.RebirthStars = state.WorkshopStars.GetValueOrDefault(workshop.Type.ToString(), 0);
        }
    }
}
