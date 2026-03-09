namespace RebornSaga.Services;

using MeineApps.Core.Ava.Services;
using RebornSaga.Models;
using System;
using System.Globalization;

/// <summary>
/// Tägliche Login-Belohnungen, Prophezeiung und Streak-Tracking.
/// Verwendet Preferences für Persistenz (kein SaveSlot-abhängiger State).
/// </summary>
public class DailyService
{
    private readonly IPreferencesService _preferences;
    private readonly GoldService _goldService;

    // Login-Belohnungen pro Tag im 7-Tage-Zyklus (Gold)
    private static readonly int[] DailyRewards = { 100, 150, 200, 250, 300, 400, 750 };

    // Prophezeiungen (zufällige ARIA-Texte)
    private static readonly string[] Prophecies =
    {
        "Die Sterne flüstern von einer großen Veränderung...",
        "Sei vorsichtig mit Entscheidungen heute. Das Schicksal beobachtet.",
        "Ein alter Freund könnte in unerwarteter Form zurückkehren.",
        "Die Dunkelheit weicht langsam. Halte durch.",
        "ARIA-Fragment: ...Zeitlinie 7.4 zeigt Anomalie bei Subjekt...",
        "Das Karma-Gleichgewicht verschiebt sich. Wähle weise.",
        "Heute ist ein guter Tag für Erkundung. Gehe neue Wege.",
        "Die Wahrheit über Nihilus liegt näher als du denkst.",
        "System-Log: Speicherintegrität bei 73%. Ursache unklar.",
        "Jemand in deiner Nähe verbirgt ein wichtiges Geheimnis.",
        "Der Weg des Schwertes und der Magie kreuzen sich bald.",
        "Vertraue deinen Verbündeten. Nicht alle Feinde sind sichtbar.",
        "ARIA-Warnung: Daten-Korruption in Sektor 9 erkannt.",
        "Die nächste Entscheidung wird weitreichende Folgen haben.",
    };

    public DailyService(IPreferencesService preferences, GoldService goldService)
    {
        _preferences = preferences;
        _goldService = goldService;
    }

    /// <summary>Aktueller Login-Streak (aufeinanderfolgende Tage).</summary>
    public int CurrentStreak => _preferences.Get("daily_streak", 0);

    /// <summary>Aktueller Tag im 7-Tage-Belohnungszyklus (0-6).</summary>
    public int CycleDay => CurrentStreak % DailyRewards.Length;

    /// <summary>Gold-Belohnung für den aktuellen Tag.</summary>
    public int TodayReward => DailyRewards[CycleDay];

    /// <summary>Ob heute bereits eingecheckt wurde.</summary>
    public bool HasClaimedToday
    {
        get
        {
            var lastClaim = _preferences.Get("daily_last_claim", "");
            return lastClaim == DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Checkt den Spieler ein und gibt die Gold-Belohnung.
    /// Gibt die Belohnungsmenge zurück (0 wenn bereits eingecheckt).
    /// </summary>
    public int ClaimDailyReward(Player player)
    {
        if (HasClaimedToday) return 0;

        // Belohnung VOR Streak-Update berechnen (CycleDay basiert auf aktuellem Streak)
        var reward = DailyRewards[CycleDay];

        // Streak prüfen: Gestern eingecheckt → Streak +1, sonst Reset
        var lastClaim = _preferences.Get("daily_last_claim", "");
        if (!string.IsNullOrEmpty(lastClaim))
        {
            var lastDate = DateTime.ParseExact(lastClaim, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var daysSince = (DateTime.Today - lastDate).Days;
            if (daysSince == 1)
            {
                // Streak fortsetzen
                _preferences.Set("daily_streak", CurrentStreak + 1);
            }
            else if (daysSince > 1)
            {
                // Streak gebrochen
                _preferences.Set("daily_streak", 1);
            }
        }
        else
        {
            // Erster Login überhaupt
            _preferences.Set("daily_streak", 1);
        }

        _goldService.AddGold(player, reward);

        // Heute als eingecheckt markieren
        _preferences.Set("daily_last_claim", DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        return reward;
    }

    /// <summary>
    /// Gibt die tägliche ARIA-Prophezeiung zurück.
    /// Basiert auf dem aktuellen Datum (deterministisch pro Tag).
    /// </summary>
    public string GetDailyProphecy()
    {
        var dayHash = DateTime.Today.GetHashCode();
        var index = ((dayHash % Prophecies.Length) + Prophecies.Length) % Prophecies.Length;
        return Prophecies[index];
    }

    /// <summary>
    /// Gibt alle 7 Belohnungen mit dem aktuellen Tag markiert zurück.
    /// Für die UI-Anzeige des Belohnungskalenders.
    /// </summary>
    public (int reward, bool isClaimed, bool isToday)[] GetRewardCalendar()
    {
        var calendar = new (int, bool, bool)[DailyRewards.Length];
        var todayCycle = CycleDay;
        var claimed = HasClaimedToday;

        for (int i = 0; i < DailyRewards.Length; i++)
        {
            // Nur der aktuelle Tag kann "claimed" sein (wenn heute eingecheckt)
            calendar[i] = (DailyRewards[i], i == todayCycle && claimed, i == todayCycle);
        }

        return calendar;
    }
}
