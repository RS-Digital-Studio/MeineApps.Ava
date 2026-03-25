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
    /// </summary>
    private static readonly (int goldenScrews, decimal moneyPercent)[] RebirthCosts =
    [
        (100, 0.10m),   // Stern 1
        (250, 0.15m),   // Stern 2
        (500, 0.20m),   // Stern 3
        (500, 0.25m),   // Stern 4 (von 750 gesenkt — F2P ~5 Wochen statt ~8)
        (1000, 0.30m),  // Stern 5 (von 1500 gesenkt — Endgame-Ziel in ~10 Wochen F2P erreichbar)
    ];

    public event EventHandler<WorkshopType>? RebirthCompleted;

    public RebirthService(
        IGameStateService gameStateService,
        IAudioService audioService,
        IPrestigeService prestigeService,
        IAscensionService ascensionService)
    {
        _gameStateService = gameStateService;
        _audioService = audioService;
        _prestigeService = prestigeService;
        _ascensionService = ascensionService;

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

        // Goldschrauben prüfen und abziehen
        if (!_gameStateService.CanAffordGoldenScrews(cost.goldenScrews))
            return false;

        _gameStateService.TrySpendGoldenScrews(cost.goldenScrews);

        // Geld-Prozent abziehen (Prozent des aktuellen Geldes)
        decimal moneyCost = state.Money * cost.moneyPercent;
        state.Money -= moneyCost;

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

        _gameStateService.MarkDirty();

        // Audio-Feedback (LevelUp-Sound passt für Rebirth-Feier)
        _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();

        RebirthCompleted?.Invoke(this, type);

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
