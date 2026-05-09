namespace BomberBlast.Services;

/// <summary>
/// Retention-Service (Phase 24 — AAA-Audit O3, O4, O5).
///
/// <para>Zentralisiert die Code-Foundation für Onboarding/FTUE-Pass:</para>
/// <list type="bullet">
///   <item>First-Win-Tracker (O3): wann hat der Spieler den 1. Sieg erzielt — triggert Cinematic-Sequenz.</item>
///   <item>FTUE-Skin-Status (O2): hat der Spieler den Free-Skin im Onboarding bereits erhalten?</item>
///   <item>Inactive-Detection (O5): Tage seit letztem Login — triggert Comeback-Reward-Bundle.</item>
///   <item>D1/D7-Window-Check (O4): aktiviert tag-spezifische Login-Bundles.</item>
/// </list>
///
/// <para>Persistenz: alle Felder via <see cref="MeineApps.Core.Ava.Services.IPreferencesService"/>.
/// Cloud-Save-Sync für FirstWinClaimed/FTUESkinClaimed (gegen App-Reinstall-Re-Trigger).</para>
/// </summary>
public interface IRetentionService
{
    /// <summary>True wenn der Spieler seinen ersten Story-Level-Sieg bereits erzielt hat.</summary>
    bool HasFirstWin { get; }

    /// <summary>
    /// True wenn der Spieler den FTUE-Free-Skin bereits eingesammelt hat. Soll false sein
    /// nach dem 1. Sieg + Erst-Spiel des Tages (Trigger-Punkt für Brawl-Stars-Style Empowerment).
    /// </summary>
    bool HasFtueSkinClaimed { get; }

    /// <summary>Tage seit letztem App-Start (0 = heute, >3 = Comeback-Trigger).</summary>
    int DaysSinceLastSession { get; }

    /// <summary>True wenn aktuell ein D1/D7-Bundle-Slot aktiv ist (Day 1 = nach 1 Tag, Day 7 = nach 7 Tagen).</summary>
    bool IsD1WindowActive { get; }
    bool IsD7WindowActive { get; }

    /// <summary>True wenn der Comeback-Reward heute eingelöst werden kann (>= 3 Tage inaktiv).</summary>
    bool IsComebackEligible { get; }

    /// <summary>Markiert den ersten Sieg als geschehen (idempotent — beim 1. Aufruf true, danach no-op).</summary>
    /// <returns>True wenn dies der ECHTE 1. Sieg war (Trigger Cinematic), false wenn schon vorher.</returns>
    bool RegisterFirstWin();

    /// <summary>Markiert den FTUE-Skin als eingelöst.</summary>
    void MarkFtueSkinClaimed();

    /// <summary>Markiert den Comeback-Reward als eingelöst (nächster Comeback braucht wieder 3+ Tage Inaktivität).</summary>
    void MarkComebackClaimed();

    /// <summary>Wird beim App-Start aufgerufen — aktualisiert LastSessionDate + DaysSinceLastSession.</summary>
    void TouchSession();
}
