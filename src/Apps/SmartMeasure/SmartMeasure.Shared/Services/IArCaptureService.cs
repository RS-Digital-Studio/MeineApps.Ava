using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Ergebnis-Status der letzten <see cref="IArCaptureService.CaptureAsync"/>-Session.
/// Erlaubt dem Caller User-Abbruch ("kein Fehler, einfach geschlossen") von echten
/// Fehlern ("Berechtigung verweigert", "ARCore nicht installiert") zu unterscheiden —
/// vorher beides als null-Result, ohne Unterscheidung.</summary>
public enum ArCaptureCompletionStatus
{
    /// <summary>Noch nie ausgefuehrt oder bewusst zurueckgesetzt.</summary>
    None = 0,
    /// <summary>Erfolgreich beendet mit gueltigem Result.</summary>
    Success = 1,
    /// <summary>User hat aktiv abgebrochen (Back-Button, kein Fehler).</summary>
    UserCancelled = 2,
    /// <summary>Fehler bei Berechtigungen, Initialisierung oder ARCore selbst.
    /// Details in <see cref="IArCaptureService.LastError"/>.</summary>
    Error = 3,
}

/// <summary>AR-Kamera-Erfassung (plattform-spezifisch: Android = ARCore, Desktop = Mock)</summary>
public interface IArCaptureService
{
    /// <summary>AR-Capture-Session starten (oeffnet native Activity, kehrt mit Ergebnis zurueck)</summary>
    Task<ArCaptureResult?> CaptureAsync();

    /// <summary>Ist ARCore auf diesem Geraet verfuegbar?</summary>
    Task<bool> IsAvailableAsync();

    /// <summary>Status der letzten <see cref="CaptureAsync"/>-Operation. Plan Kap. 4.3:
    /// erlaubt dem UI-Layer Abbruch von Fehler zu trennen, ohne raten zu muessen.</summary>
    ArCaptureCompletionStatus LastCompletionStatus { get; }

    /// <summary>Lokalisierter Fehlertext der letzten Session falls
    /// <see cref="LastCompletionStatus"/> = <see cref="ArCaptureCompletionStatus.Error"/>.
    /// Bei <see cref="ArCaptureCompletionStatus.UserCancelled"/> null.</summary>
    string? LastError { get; }

    /// <summary>Plan-Kap. 5.9: Stakeout-Targets fuer die naechste AR-Session vorbereiten.
    /// Muss vor <see cref="CaptureAsync"/> gesetzt werden. Wird beim Activity-Start
    /// uebernommen. Null = Stakeout-Mode in der Activity ist leer (Hint statt Pfeil).</summary>
    void SetStakeoutTargets(IReadOnlyList<StakeoutTarget>? targets);

    /// <summary>Plan-Kap. 5.2: Persistente Sites — bestehende Projekt-Punkte werden in der
    /// AR-Session als Earth-Anchors visualisiert (graue Marker), damit der User neue
    /// Punkte im selben Koordinatensystem erfasst. Funktioniert ueber den Earth-Anchor-
    /// Cache (Geospatial-API), kostenlos solange VPS-Coverage vorhanden ist. Site-Punkte
    /// gehen NICHT ins ArCaptureResult zurueck.</summary>
    void SetSitePoints(IReadOnlyList<SurveyPoint>? points);
}
