using BomberBlast.Models.Entities;

namespace BomberBlast.Services;

/// <summary>
/// Weekly Boss-Rush (v2.0.41, Plan Task 3.3).
///
/// Alle 5 Boss-Typen hintereinander gegen die Uhr. Wochen-Best-Score wird persistiert.
/// Run startet via <see cref="StartRun"/>, GameEngine sequenziert die 5 Bosse intern
/// (Heal + Floating-Text zwischen den Bossen, Score akkumuliert ueber den gesamten Run).
///
/// Persistente Wochen-Statistiken:
/// <list type="bullet">
///   <item><see cref="WeeklyBestScore"/> + <see cref="WeeklyBestTime"/> — beste Performance dieser Woche.</item>
///   <item><see cref="LastWeekId"/> — Wochen-Reset-Marker (ISO 8601 Year-Week, z.B. "2026-W18").</item>
///   <item><see cref="HasRunThisWeek"/> — true wenn schon mindestens 1 Versuch dieser Woche.</item>
/// </list>
/// </summary>
public interface IBossRushService
{
    /// <summary>Liste aller 5 Bosse in fixer Reihenfolge: StoneGolem → IceDragon → FireDemon → ShadowMaster → FinalBoss.</summary>
    IReadOnlyList<BossType> BossSequence { get; }

    /// <summary>Beste Score dieser Woche (0 = noch nicht gespielt).</summary>
    int WeeklyBestScore { get; }

    /// <summary>Beste Zeit dieser Woche in Sekunden (0 = noch nicht abgeschlossen).</summary>
    float WeeklyBestTime { get; }

    /// <summary>Wochen-ID des letzten persistierten Wertes (Format "yyyy-Www").</summary>
    string LastWeekId { get; }

    /// <summary>True wenn mindestens 1 Run in der aktuellen Woche aufgezeichnet wurde.</summary>
    bool HasRunThisWeek { get; }

    /// <summary>True wenn der Spieler den Boss-Rush jemals abgeschlossen hat (alle 5 Bosse).</summary>
    bool HasEverCompleted { get; }

    /// <summary>Anzahl Boss-Rush-Abschluesse insgesamt (lifetime).</summary>
    int TotalCompletions { get; }

    /// <summary>Aktuelles Wochen-ID-Format (Format "yyyy-Www" basierend auf UTC-Datum).</summary>
    string CurrentWeekId { get; }

    /// <summary>
    /// Run-Resultat melden — wird gegen WeeklyBest und LifetimeBest gepruefft. Bei Wochen-Wechsel
    /// wird der Best-Wert resettet und die ID aktualisiert.
    /// </summary>
    /// <param name="finalScore">Akkumulierter Score nach allen 5 Bossen.</param>
    /// <param name="totalTimeSeconds">Gesamt-Spielzeit in Sekunden.</param>
    /// <param name="completedAllBosses">True wenn alle 5 Bosse besiegt (false bei Tod).</param>
    /// <returns>True wenn neuer Wochen-Best erzielt.</returns>
    bool SubmitRun(int finalScore, float totalTimeSeconds, bool completedAllBosses);
}
