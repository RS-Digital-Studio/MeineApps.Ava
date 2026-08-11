using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>
/// Passt die Punkte einer FOLGE-Sitzung auf den Bestand des Projekts ein.
///
/// Warum das nötig ist: Jede ARCore-Sitzung hat ihren eigenen Anker (Geo-Pose des
/// Session-Ursprungs, ±1–3 m via VPS bzw. ±3–8 m via Handy-GPS) und ihr eigenes Heading
/// (±5° via VPS, ±15–30° via Magnetometer). Die Messung IN der Sitzung ist zentimetergenau,
/// aber der ganze Satz liegt gegenüber der Vorsitzung verschoben UND verdreht — bei 20 m
/// Ausdehnung sind 5° Heading-Fehler bereits 1,7 m Querversatz.
///
/// Verfahren: Punkte, die dieselbe Stelle erneut messen (Wiederholungsmessungen), werden als
/// Passpunkt-Paare erkannt und daraus eine 2D-Helmert-Transformation (Translation + Rotation)
/// im Sinne kleinster Quadrate bestimmt. **Ohne Maßstabsfaktor** — der Maßstab kommt aus der
/// AR-Geometrie und ist gut; ihn mitzuschätzen würde echte Messung gegen Anker-Rauschen tauschen.
/// </summary>
public interface ISessionRegistrationService
{
    /// <summary>
    /// Bestimmt die Einpassung der neuen Punkte auf den Bestand und liefert die transformierten
    /// Punkte. Lehnt ab (<see cref="SessionRegistrationResult.Applied"/> = false), wenn zu wenige
    /// Passpunkte gefunden wurden oder die Restklaffung zu groß ist — dann bleiben die Punkte
    /// unverändert, statt sie auf eine falsche Paarung zu ziehen.
    /// </summary>
    /// <param name="existing">Bestandspunkte des Projekts (vor dem Transfer).</param>
    /// <param name="incoming">Neu übertragene Punkte derselben Sitzung.</param>
    /// <param name="options">Toleranzen; null = <see cref="SessionRegistrationOptions.Default"/>.</param>
    SessionRegistrationResult Register(
        IReadOnlyList<SurveyPoint> existing,
        IReadOnlyList<SurveyPoint> incoming,
        SessionRegistrationOptions? options = null);
}

/// <summary>Toleranzen der Sitzungs-Einpassung.</summary>
/// <param name="MatchRadiusMeters">
/// Maximaler Abstand, bis zu dem ein neuer Punkt als Wiederholungsmessung eines Bestandspunkts
/// gilt. Muss die erwartete Anker-/Heading-Abweichung abdecken (Default 3 m), darf aber nicht so
/// groß sein, dass benachbarte echte Punkte (Zaunpfosten!) verwechselt werden.
/// </param>
/// <param name="MinPairs">
/// Mindestzahl an Passpunkt-Paaren. 2 ist das Minimum für Translation + Rotation; mit nur einem
/// Paar wäre die Rotation unbestimmt.
/// </param>
/// <param name="MaxResidualMeters">
/// Obergrenze der Restklaffung (RMS) nach dem Ausgleich. Wird sie überschritten, passen die Paare
/// nicht zusammen (Fehlzuordnung) und die Einpassung wird verworfen.
/// </param>
/// <param name="MaxRotationDeg">
/// Obergrenze der geschätzten Drehung. Mehr als das ist kein Heading-Fehler mehr, sondern eine
/// Fehlzuordnung — VPS-Heading liegt bei ±5°, Magnetometer im Extremfall bei ±30°.
/// </param>
public sealed record SessionRegistrationOptions(
    double MatchRadiusMeters = 3.0,
    int MinPairs = 2,
    double MaxResidualMeters = 0.5,
    double MaxRotationDeg = 45.0)
{
    public static SessionRegistrationOptions Default { get; } = new();
}

/// <summary>Ergebnis der Sitzungs-Einpassung.</summary>
/// <param name="Applied">Wurde eine Transformation angewandt?</param>
/// <param name="Points">
/// Die (ggf. transformierten) Punkte. Bei <paramref name="Applied"/> = false identisch zur Eingabe.
/// </param>
/// <param name="PairCount">Anzahl verwendeter Passpunkt-Paare.</param>
/// <param name="RotationDeg">Geschätzte Drehung in Grad (positiv = gegen den Uhrzeigersinn).</param>
/// <param name="TranslationMeters">Länge des Verschiebungsvektors in Metern.</param>
/// <param name="ResidualRmsMeters">Restklaffung nach dem Ausgleich (RMS über die Paare).</param>
/// <param name="Rejection">Ablehnungsgrund, wenn nicht angewandt.</param>
public sealed record SessionRegistrationResult(
    bool Applied,
    List<SurveyPoint> Points,
    int PairCount,
    double RotationDeg,
    double TranslationMeters,
    double ResidualRmsMeters,
    SessionRegistrationRejection Rejection);

/// <summary>Warum eine Einpassung nicht angewandt wurde.</summary>
public enum SessionRegistrationRejection
{
    /// <summary>Kein Grund — die Einpassung wurde angewandt.</summary>
    None = 0,
    /// <summary>Projekt hatte noch keine Punkte (erste Sitzung — es gibt nichts einzupassen).</summary>
    NoExistingPoints,
    /// <summary>Zu wenige Passpunkt-Paare innerhalb des Suchradius.</summary>
    TooFewPairs,
    /// <summary>Restklaffung über der Toleranz — die Paare passen nicht zusammen.</summary>
    ResidualTooLarge,
    /// <summary>Geschätzte Drehung unplausibel groß — vermutlich Fehlzuordnung.</summary>
    RotationImplausible,
    /// <summary>Passpunkte liegen (fast) auf einem Punkt — Rotation daraus nicht bestimmbar.</summary>
    DegenerateGeometry,
}
