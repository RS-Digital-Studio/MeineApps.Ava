namespace RebornSaga.Services;

using RebornSaga.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Verwaltet Kapitel-Freischaltung per Gold.
/// Prolog (P1-P3) + Arc 1 K1-K5 gratis, K6-K10 kosten Gold.
/// Freischaltungen gelten global für alle Save-Slots.
/// </summary>
public class ChapterUnlockService
{
    private readonly SaveGameService _saveService;
    private readonly GoldService _goldService;
    private readonly SemaphoreSlim _unlockSemaphore = new(1, 1); // Verhindert doppelte Freischaltung bei schnellem Doppel-Tap

    /// <summary>
    /// Kosten pro Premium-Kapitel (K6-K10).
    /// </summary>
    private static readonly Dictionary<string, int> ChapterCosts = new()
    {
        ["k6"] = 2000,
        ["k7"] = 3000,
        ["k8"] = 4000,
        ["k9"] = 5000,
        ["k10"] = 6000
    };

    /// <summary>
    /// Gratis-Kapitel die immer verfügbar sind.
    /// </summary>
    private static readonly HashSet<string> FreeChapters = new()
    {
        "p1", "p2", "p3", // Prolog
        "k1", "k2", "k3", "k4", "k5" // Arc 1 (gratis)
    };

    /// <summary>
    /// Event wenn ein Kapitel freigeschaltet wird (chapterId).
    /// </summary>
    public event Action<string>? ChapterUnlocked;

    public ChapterUnlockService(SaveGameService saveService, GoldService goldService)
    {
        _saveService = saveService;
        _goldService = goldService;
    }

    /// <summary>
    /// Prüft ob ein Kapitel freigeschaltet ist (gratis oder gekauft).
    /// </summary>
    public async Task<bool> IsUnlockedAsync(string chapterId)
    {
        if (FreeChapters.Contains(chapterId))
            return true;

        return await _saveService.IsChapterUnlockedAsync(chapterId);
    }

    /// <summary>
    /// Gibt die Gold-Kosten eines Kapitels zurück (0 = gratis).
    /// </summary>
    public static int GetCost(string chapterId)
    {
        return ChapterCosts.GetValueOrDefault(chapterId, 0);
    }

    /// <summary>
    /// Prüft ob sich der Spieler das Kapitel leisten kann.
    /// </summary>
    public bool CanAfford(string chapterId, Player player)
    {
        var cost = GetCost(chapterId);
        return cost == 0 || player.Gold >= cost;
    }

    /// <summary>
    /// Schaltet ein Kapitel mit Gold frei. Gibt false zurück wenn nicht möglich.
    /// SemaphoreSlim verhindert Race Condition bei doppeltem Aufruf (z.B. schneller Doppel-Tap).
    /// </summary>
    public async Task<bool> UnlockWithGoldAsync(string chapterId, Player player)
    {
        if (FreeChapters.Contains(chapterId)) return true;

        await _unlockSemaphore.WaitAsync();
        try
        {
            // Bereits freigeschaltet? Kein Gold abziehen
            if (await _saveService.IsChapterUnlockedAsync(chapterId)) return true;

            var cost = GetCost(chapterId);
            if (cost <= 0) return false; // Unbekanntes Kapitel

            if (!_goldService.SpendGold(player, cost))
                return false;

            await _saveService.UnlockChapterAsync(chapterId, "gold");
            ChapterUnlocked?.Invoke(chapterId);
            return true;
        }
        finally
        {
            _unlockSemaphore.Release();
        }
    }

    /// <summary>
    /// Gibt alle Premium-Kapitel mit Kosten und Status zurück (ein DB-Query).
    /// </summary>
    public async Task<List<(string chapterId, int cost, bool unlocked)>> GetPremiumChaptersAsync()
    {
        var unlocked = await _saveService.GetUnlockedChaptersAsync();
        var unlockedIds = new HashSet<string>();
        foreach (var c in unlocked)
            unlockedIds.Add(c.ChapterId);

        var result = new List<(string, int, bool)>();
        foreach (var (chapterId, cost) in ChapterCosts)
            result.Add((chapterId, cost, unlockedIds.Contains(chapterId)));
        return result;
    }

    /// <summary>
    /// Gibt die Gesamtkosten aller noch gesperrten Premium-Kapitel zurück (ein DB-Query).
    /// </summary>
    public async Task<int> GetTotalRemainingCostAsync()
    {
        var unlocked = await _saveService.GetUnlockedChaptersAsync();
        var unlockedIds = new HashSet<string>();
        foreach (var c in unlocked)
            unlockedIds.Add(c.ChapterId);

        var total = 0;
        foreach (var (chapterId, cost) in ChapterCosts)
        {
            if (!unlockedIds.Contains(chapterId))
                total += cost;
        }
        return total;
    }
}
