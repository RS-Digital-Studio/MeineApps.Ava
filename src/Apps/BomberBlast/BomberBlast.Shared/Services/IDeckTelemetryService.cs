using BomberBlast.Models.Entities;

namespace BomberBlast.Services;

/// <summary>
/// Balancing-Telemetrie für das Deck-System: Tracked Usage- und Win-Rate pro
/// Spezial-Bombentyp, damit Balance-Targets (keine Karte &lt;5% Usage, keine
/// &gt;40%) empirisch überprüfbar sind.
///
/// <para>Persistiert lokale Counter in Preferences (JSON). Kann optional an
/// Firebase Realtime DB hochgeladen werden (<see cref="FlushToRemoteAsync"/>),
/// Aggregation pro User-UID.</para>
///
/// <para><b>Warum lokal zuerst:</b> Firebase-Write pro Bomben-Platzierung wäre
/// Traffic-Overkill. Lokale Aggregation + periodischer Upload (z.B. einmal
/// pro App-Start) reichen für Balancing-Audit.</para>
/// </summary>
public interface IDeckTelemetryService : IDisposable
{
    /// <summary>
    /// Wird aufgerufen wenn der Spieler eine Spezial-Bombe platziert (nicht Normal).
    /// Incrementiert den Used-Counter für den Typ.
    /// </summary>
    void RecordBombPlaced(BombType type);

    /// <summary>
    /// Wird aufgerufen wenn ein Level abgeschlossen wird, mit der Liste der
    /// tatsächlich während des Levels eingesetzten Spezial-Bombentypen.
    /// Jeder Typ bekommt +1 Win (win-rate pro Card-Typ).
    /// </summary>
    /// <param name="typesUsed">Eindeutige Bombentypen, die gespielt wurden.</param>
    void RecordLevelCompletedWithBombs(IEnumerable<BombType> typesUsed);

    /// <summary>
    /// Wird aufgerufen wenn der Spieler ein Level startet und währenddessen
    /// mindestens eine Spezial-Bombe einsetzt. Incrementiert den Plays-Counter
    /// (für Win-Rate = Wins/Plays).
    /// </summary>
    void RecordLevelStartedWithBombs(IEnumerable<BombType> typesUsed);

    /// <summary>
    /// Aggregierte Statistik pro Bomben-Typ.
    /// Used = Gesamt-Platzierungen, Plays = Level-Starts mit dieser Karte,
    /// Wins = Level-Completes mit dieser Karte.
    /// </summary>
    IReadOnlyDictionary<BombType, DeckTelemetryEntry> GetStats();

    /// <summary>
    /// Setzt alle Counter zurück (z.B. für neues Balancing-Fenster).
    /// </summary>
    void Reset();

    /// <summary>
    /// Lädt die aggregierten Counter nach Firebase hoch (Path: analytics/deck/{uid}).
    /// Fire-and-forget geeignet — wirft nicht, loggt nur.
    /// </summary>
    Task FlushToRemoteAsync();
}

/// <summary>
/// Aggregierte Telemetrie-Werte für einen Bomben-Typ.
/// </summary>
public sealed class DeckTelemetryEntry
{
    public int Used { get; set; }
    public int Plays { get; set; }
    public int Wins { get; set; }

    /// <summary>Win-Rate 0.0-1.0 (0 wenn Plays=0).</summary>
    public double WinRate => Plays > 0 ? (double)Wins / Plays : 0.0;
}
