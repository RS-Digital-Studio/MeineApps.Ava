using System.Text.Json;
using BomberBlast.Models;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Abstrakte Basisklasse für zeitgesteuerte Missions-Services (täglich/wöchentlich).
/// Enthält die gemeinsame Logik: Missions-Generierung, Fortschritts-Tracking,
/// Persistenz (Load/Save) und Perioden-Reset.
///
/// WeeklyMission wird als generischer Missions-Typ verwendet (nicht nur für wöchentliche Missionen).
/// </summary>
public abstract class TimedMissionServiceBase
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly IPreferencesService _preferences;
    protected readonly IBattlePassService BattlePassService;
    protected readonly ILeagueService LeagueService;

    private MissionPersistenceData _data;
    private List<WeeklyMission> _missions;

    protected TimedMissionServiceBase(IPreferencesService preferences, IBattlePassService battlePassService, ILeagueService leagueService)
    {
        _preferences = preferences;
        BattlePassService = battlePassService;
        LeagueService = leagueService;
        _data = Load();
        _missions = [];
        CheckPeriodReset();
    }

    // --- Abstrakte Methoden/Properties, die jeder Subtyp definiert ---

    /// <summary>Preferences-Schlüssel für die Persistenz (z.B. "DailyMissionData")</summary>
    protected abstract string PreferencesKey { get; }

    /// <summary>Anzahl der Missionen pro Periode (3 für täglich, 5 für wöchentlich)</summary>
    protected abstract int MissionsPerPeriod { get; }

    /// <summary>Bonus-Coins wenn alle Missionen einer Periode abgeschlossen sind</summary>
    protected abstract int AllCompleteBonusCoinAmount { get; }

    /// <summary>Missions-Pool mit Typen, Lokalisierungs-Keys, Target-Bereichen und Belohnungen</summary>
    protected abstract (WeeklyMissionType Type, string Name, string Desc, int Min, int Max, int Reward)[] GetMissionPool();

    /// <summary>Perioden-ID berechnen (Tag-ID oder Wochen-ID aus dem aktuellen Datum)</summary>
    protected abstract int GetPeriodId(DateTime date);

    /// <summary>Nächstes Reset-Datum (nächster Tag oder nächster Montag)</summary>
    public abstract DateTime NextResetDate { get; }

    /// <summary>Wird aufgerufen wenn eine Mission durch Tracking abgeschlossen wird</summary>
    protected abstract void OnMissionCompleted();

    /// <summary>Wird aufgerufen wenn der All-Complete-Bonus beansprucht wird</summary>
    protected abstract void OnAllCompleteBonusClaimed();

    // --- Gemeinsame Properties ---

    /// <summary>Aktuelle Missionen dieser Periode</summary>
    public List<WeeklyMission> MissionsList => _missions;

    /// <summary>Ob alle Missionen der aktuellen Periode abgeschlossen sind</summary>
    public bool IsAllComplete => _missions.Count > 0 && _missions.All(m => m.IsCompleted);

    /// <summary>Anzahl abgeschlossener Missionen</summary>
    public int CompletedCount => _missions.Count(m => m.IsCompleted);

    /// <summary>Bonus-Coins für alle-abgeschlossen</summary>
    public int AllCompleteBonusCoins => AllCompleteBonusCoinAmount;

    /// <summary>Ob der All-Complete-Bonus bereits eingesammelt wurde</summary>
    public bool IsBonusClaimed => _data.BonusClaimed;

    /// <summary>Gesamtanzahl abgeschlossener Perioden</summary>
    public int TotalPeriodsCompleted => _data.TotalPeriodsCompleted;

    // --- Gemeinsame Methoden ---

    /// <summary>
    /// Fortschritt für einen Missionstyp tracken.
    /// Gibt true zurück wenn eine Mission dadurch abgeschlossen wurde.
    /// </summary>
    public bool TrackProgressInternal(WeeklyMissionType type, int amount = 1)
    {
        if (amount <= 0) return false;

        bool anyCompleted = false;
        foreach (var mission in _missions)
        {
            if (mission.Type == type && !mission.IsCompleted)
            {
                mission.CurrentCount = Math.Min(mission.CurrentCount + amount, mission.TargetCount);
                if (mission.IsCompleted)
                    anyCompleted = true;
            }
        }

        if (anyCompleted || amount > 0)
        {
            // Fortschritt in Data synchronisieren
            SyncMissionsToData();
            Save();
        }

        // Subtyp-spezifische Aktion bei abgeschlossener Mission
        if (anyCompleted)
            OnMissionCompleted();

        return anyCompleted;
    }

    /// <summary>
    /// All-Complete-Bonus einsammeln.
    /// Gibt die Bonus-Coins zurück (0 wenn bereits eingesammelt oder nicht alle fertig).
    /// </summary>
    public int ClaimAllCompleteBonus()
    {
        if (!IsAllComplete || _data.BonusClaimed) return 0;

        _data.BonusClaimed = true;
        _data.TotalPeriodsCompleted++;
        Save();

        // Subtyp-spezifische Bonus-Aktionen (XP, Liga-Punkte, Gems)
        OnAllCompleteBonusClaimed();

        return AllCompleteBonusCoinAmount;
    }

    /// <summary>
    /// Prüft ob eine neue Periode angefangen hat und generiert ggf. neue Missionen
    /// </summary>
    private void CheckPeriodReset()
    {
        var currentPeriodId = GetPeriodId(DateTime.UtcNow);

        if (_data.PeriodId != currentPeriodId)
        {
            // Neue Periode → neue Missionen generieren
            _data.PeriodId = currentPeriodId;
            _data.BonusClaimed = false;
            _data.MissionProgress = [];
            GenerateMissions(currentPeriodId);
            SyncMissionsToData();
            Save();
        }
        else
        {
            // Bestehende Missionen aus gespeicherten Daten wiederherstellen
            RestoreMissionsFromData(currentPeriodId);
        }
    }

    /// <summary>
    /// Missionen deterministisch aus der Perioden-ID generieren (keine Duplikate).
    /// Gleiche ID = gleiche Missionen (deterministischer Seed).
    /// </summary>
    private void GenerateMissions(int periodId)
    {
        var pool = GetMissionPool();
        var rng = new Random(periodId);
        _missions = [];

        // Pool mischen und erste N nehmen
        var indices = Enumerable.Range(0, pool.Length).ToList();
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        for (int i = 0; i < MissionsPerPeriod && i < indices.Count; i++)
        {
            var template = pool[indices[i]];
            // Target zufällig zwischen Min und Max
            int target = template.Min + rng.Next(template.Max - template.Min + 1);
            // Auf "schöne" Zahlen runden (5er-Schritte nur für größere Targets)
            if (target >= 10)
                target = (target / 5) * 5;

            _missions.Add(new WeeklyMission
            {
                Type = template.Type,
                NameKey = template.Name,
                DescriptionKey = template.Desc,
                TargetCount = Math.Max(target, template.Min),
                CurrentCount = 0,
                CoinReward = template.Reward,
            });
        }
    }

    /// <summary>
    /// Missionen aus gespeicherten Daten wiederherstellen (deterministisch + gespeicherter Fortschritt)
    /// </summary>
    private void RestoreMissionsFromData(int periodId)
    {
        // Missionen neu generieren (deterministisch gleich)
        GenerateMissions(periodId);

        // Gespeicherten Fortschritt anwenden
        if (_data.MissionProgress != null)
        {
            for (int i = 0; i < _missions.Count && i < _data.MissionProgress.Count; i++)
            {
                _missions[i].CurrentCount = _data.MissionProgress[i];
            }
        }
    }

    /// <summary>
    /// Fortschritt der Missionen in die Persistenz-Daten synchronisieren
    /// </summary>
    private void SyncMissionsToData()
    {
        _data.MissionProgress = _missions.Select(m => m.CurrentCount).ToList();
    }

    private MissionPersistenceData Load()
    {
        try
        {
            string json = _preferences.Get<string>(PreferencesKey, "");
            if (!string.IsNullOrEmpty(json))
                return JsonSerializer.Deserialize<MissionPersistenceData>(json, JsonOptions) ?? new MissionPersistenceData();
        }
        catch
        {
            // Fehler beim Laden → Standardwerte
        }
        return new MissionPersistenceData();
    }

    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_data, JsonOptions);
            _preferences.Set(PreferencesKey, json);
        }
        catch
        {
            // Speichern fehlgeschlagen
        }
    }

    /// <summary>
    /// Generische Persistenz-Daten für zeitgesteuerte Missionen.
    /// Wird für täglich und wöchentlich identisch verwendet.
    /// ACHTUNG: JSON-Property-Namen bleiben generisch - bestehende Saves mit
    /// DayId/WeekId werden durch die Perioden-Migration automatisch als "neue Periode"
    /// erkannt und frisch generiert (gewünschtes Verhalten bei Perioden-Wechsel).
    /// </summary>
    private class MissionPersistenceData
    {
        public int PeriodId { get; set; }
        public List<int>? MissionProgress { get; set; }
        public bool BonusClaimed { get; set; }
        public int TotalPeriodsCompleted { get; set; }
    }
}
