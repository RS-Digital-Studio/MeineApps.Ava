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

    // Hot-Path-Schutz: Im Gameplay feuert TrackProgress pro Gegner-Kill/Combo/PowerUp (mehrfach
    // pro Frame bei Ketten-Explosionen). Ohne Debounce serialisierte jeder Aufruf das JSON synchron
    // auf dem UI-Thread → aufgestauter Gen0-Muell → periodische GC-Pause (sichtbarer Stutter auf
    // Mono-AOT-Android). Dirty-Flag + Debounce wie AchievementService; FlushIfDirty() am Level-Ende
    // (GameTrackingService.FlushIfDirty) erzwingt den finalen Save.
    private const int SaveDebounceMs = 1500;
    private bool _isDirty;
    private DateTime _lastSaveTime = DateTime.MinValue;

    private readonly IPreferencesService _preferences;
    protected readonly IBattlePassService BattlePassService;
    protected readonly ILeagueService LeagueService;

    private MissionPersistenceData _data;
    private List<WeeklyMission> _missions;
    // Lazy-Init-Flag: CheckPeriodReset() darf NICHT im Base-Ctor laufen, weil es virtuelle
    // Properties (z.B. CurrentPlayerLevel) aufruft, die im Subtyp auf injizierte Services
    // zugreifen — diese sind beim Base-Ctor-Aufruf noch null (Subtyp-Felder werden erst
    // NACH base() zugewiesen). Initialisierung erfolgt deshalb beim ersten Zugriff.
    private bool _initialized;

    protected TimedMissionServiceBase(IPreferencesService preferences, IBattlePassService battlePassService, ILeagueService leagueService)
    {
        _preferences = preferences;
        BattlePassService = battlePassService;
        LeagueService = leagueService;
        _data = Load();
        _missions = [];
    }

    /// <summary>
    /// Stellt sicher dass Missionen für die aktuelle Periode geladen/generiert sind.
    /// Muss VOR jedem Zugriff auf _missions/_data laufen — wird von allen public APIs aufgerufen.
    /// Idempotent: subsequent Calls sind No-Ops.
    /// </summary>
    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
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

    /// <summary>
    /// v2.0.60 (B-D3): Optionales Player-Level für Mission-Filtering. Wird vom Subtyp
    /// gesetzt damit unerreichbare Missionen (z.B. "Complete 3-Star Levels" auf L10-Account)
    /// nicht in den Pool aufgenommen werden. Default 1 = kein Gating.
    /// </summary>
    protected virtual int CurrentPlayerLevel => 1;

    /// <summary>
    /// v2.0.60 (B-D3): Returns minimum player level required for this mission type.
    /// Wird im GenerateMissions-Filter angewendet.
    /// </summary>
    protected static int GetMinLevelForMissionType(WeeklyMissionType type) => type switch
    {
        // Skill-Missionen brauchen L30+ (3-Star auf Account ohne Sterne-Optimierung unmöglich).
        WeeklyMissionType.CompleteThreeStar => 30,
        WeeklyMissionType.NoDamageLevel => 30,
        WeeklyMissionType.CompleteMutatorLevel => 60,  // Mutator ab Welt 5 (L41+), aber sinnvoll erst ab Welt 7
        // Endgame-Features brauchen Unlock.
        WeeklyMissionType.WinBossFights => 10,        // Erster Boss
        WeeklyMissionType.CompleteDungeonFloors => 20,
        WeeklyMissionType.UseSpecialBombs => 15,      // Special Bombs unlocked
        WeeklyMissionType.UpgradeCards => 20,
        WeeklyMissionType.PlayQuickPlay => 1,
        WeeklyMissionType.SpinLuckyWheel => 1,
        // Standard-Missionen ab L1 möglich.
        _ => 1
    };

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
    public List<WeeklyMission> MissionsList { get { EnsureInitialized(); return _missions; } }

    /// <summary>Ob alle Missionen der aktuellen Periode abgeschlossen sind</summary>
    public bool IsAllComplete { get { EnsureInitialized(); return _missions.Count > 0 && _missions.All(m => m.IsCompleted); } }

    /// <summary>Anzahl abgeschlossener Missionen</summary>
    public int CompletedCount { get { EnsureInitialized(); return _missions.Count(m => m.IsCompleted); } }

    /// <summary>Bonus-Coins für alle-abgeschlossen</summary>
    public int AllCompleteBonusCoins => AllCompleteBonusCoinAmount;

    /// <summary>Ob der All-Complete-Bonus bereits eingesammelt wurde</summary>
    public bool IsBonusClaimed { get { EnsureInitialized(); return _data.BonusClaimed; } }

    /// <summary>Gesamtanzahl abgeschlossener Perioden</summary>
    public int TotalPeriodsCompleted { get { EnsureInitialized(); return _data.TotalPeriodsCompleted; } }

    // --- Gemeinsame Methoden ---

    /// <summary>
    /// Fortschritt für einen Missionstyp tracken.
    /// Gibt true zurück wenn eine Mission dadurch abgeschlossen wurde.
    /// </summary>
    public bool TrackProgressInternal(WeeklyMissionType type, int amount = 1)
    {
        if (amount <= 0) return false;
        EnsureInitialized();

        bool anyCompleted = false;
        bool changed = false;
        foreach (var mission in _missions)
        {
            if (mission.Type == type && !mission.IsCompleted)
            {
                int newCount = Math.Min(mission.CurrentCount + amount, mission.TargetCount);
                if (newCount != mission.CurrentCount)
                {
                    mission.CurrentCount = newCount;
                    changed = true;
                }
                if (mission.IsCompleted)
                    anyCompleted = true;
            }
        }

        // Nur persistieren wenn sich wirklich ein Fortschritt geaendert hat. Der fruehere
        // "anyCompleted || amount > 0"-Guard war wegen amount-Default=1 IMMER wahr und serialisierte
        // JEDEN Kill 2x JSON — auch wenn keine Mission dieses Typs aktiv/offen war (Stutter-Wurzel).
        if (changed)
        {
            SyncMissionsToData();
            MarkDirty();
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
        EnsureInitialized();
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
        var fullPool = GetMissionPool();
        var rng = new Random(periodId);
        _missions = [];

        // v2.0.60 (B-D3): Pool filtern nach Player-Level. Unerreichbare Missionen
        // (z.B. "3-Star Level" auf L10-Account) werden ausgeschlossen — sonst frustriert
        // der Spieler, dass die Belohnung unerreichbar ist.
        int playerLevel = CurrentPlayerLevel;
        var pool = fullPool.Where(t => GetMinLevelForMissionType(t.Type) <= playerLevel).ToArray();

        // Falls der Filter zu wenige Missionen übrig lässt, Fallback auf full pool —
        // gilt vor allem für Neulings-Accounts auf L1-9, die fast nur Standard-Missionen sehen.
        if (pool.Length < MissionsPerPeriod)
            pool = fullPool;

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

    /// <summary>
    /// Markiert die Daten als geaendert und speichert nur, wenn das Debounce-Fenster ueberschritten
    /// ist. Verhindert den JSON-Serialize-Sturm im Gameplay-Hot-Path (Kill/Combo/PowerUp pro Frame).
    /// </summary>
    private void MarkDirty()
    {
        _isDirty = true;
        if ((DateTime.UtcNow - _lastSaveTime).TotalMilliseconds >= SaveDebounceMs)
            Save();
    }

    /// <summary>
    /// Erzwingt das Speichern falls das Dirty-Flag gesetzt ist (z.B. am Level-Ende ueber
    /// GameTrackingService.FlushIfDirty oder beim App-Background-Hook).
    /// </summary>
    public void FlushIfDirty()
    {
        if (_isDirty) Save();
    }

    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_data, JsonOptions);
            _preferences.Set(PreferencesKey, json);
            _isDirty = false;
            _lastSaveTime = DateTime.UtcNow;
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
