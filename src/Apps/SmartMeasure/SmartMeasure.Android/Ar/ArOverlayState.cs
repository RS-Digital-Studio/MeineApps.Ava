namespace SmartMeasure.Android.Ar;

/// <summary>
/// Lokalisierte AR-Overlay-Labels (Plan-Kap. 4.11). Wird einmal pro Session in
/// <see cref="ArCaptureActivity"/> aus dem aktuellen <c>AppStrings</c>-Stand erzeugt
/// und an alle <see cref="ArOverlayState"/>-Snapshots durchgereicht. Sprachwechsel
/// passieren nie mid-session — ein Snapshot reicht.
/// </summary>
public sealed record ArOverlayLabels(
    string Points,
    string Area,
    string Length,
    string HeightDelta,
    string Anchors,
    string Time,
    string HoldStill,
    string Ready,
    string TrackingLost,
    string TrackingInsufficientLight,
    string TrackingInsufficientFeatures,
    string TrackingExcessiveMotion,
    string TrackingCameraUnavailable,
    string TrackingBadState)
{
    /// <summary>Hardcoded DE-Fallback fuer Activity-Konstruktion, bevor Localization-Service
    /// verfuegbar ist (z.B. erster Frame vor BuildOverlayState).</summary>
    public static ArOverlayLabels GermanDefaults { get; } = new(
        Points: "PUNKTE",
        Area: "FLÄCHE",
        Length: "LÄNGE",
        HeightDelta: "ΔH",
        Anchors: "ANKER",
        Time: "ZEIT",
        HoldStill: "STILL HALTEN",
        Ready: "BEREIT",
        TrackingLost: "Tracking verloren",
        TrackingInsufficientLight: "Nicht genug Licht",
        TrackingInsufficientFeatures: "Mehr Texturen/Kanten nötig",
        TrackingExcessiveMotion: "Langsamer bewegen",
        TrackingCameraUnavailable: "Kamera nicht verfügbar",
        TrackingBadState: "Session-Fehler");
}

/// <summary>
/// Qualität eines AR-HitTest-Ergebnisses. Bestimmt Reticle-Farbe und Punkt-Confidence.
/// </summary>
public enum ArHitQuality
{
    /// <summary>Kein HitResult — Reticle rot, kein Punkt platzierbar.</summary>
    None,

    /// <summary>Instant Placement ohne Plane (Schätzung) — Reticle gelb, Confidence ~0.5.</summary>
    InstantPlacement,

    /// <summary>Feature-Point-Hit ohne Plane — Reticle orange, Confidence ~0.6.</summary>
    Point,

    /// <summary>Plane-Hit im Polygon — Reticle grün, Confidence ~0.9.</summary>
    Plane
}

/// <summary>
/// Live-Zustand des AR-Overlays, wird vom GL-Thread pro Frame aktualisiert
/// und vom UI-Thread (OnDraw) gelesen. Alle Mutationen atomar via Referenz-Swap.
/// </summary>
public sealed class ArOverlayState
{
    /// <summary>Ist die Kamera im Tracking-Mode?</summary>
    public bool IsTracking { get; init; } = true;

    /// <summary>
    /// Grund für Tracking-Verlust (falls <see cref="IsTracking"/> false).
    /// Lokalisierter Kurztext wie "Nicht genug Licht".
    /// </summary>
    public string? TrackingFailureReason { get; init; }

    /// <summary>Aktuelle Reticle-Position (Bildmitte oder HitTest-Ziel).</summary>
    public float ReticleX { get; init; }
    public float ReticleY { get; init; }

    /// <summary>Qualität des aktuellen Reticle-HitTests.</summary>
    public ArHitQuality HitQuality { get; init; } = ArHitQuality.None;

    /// <summary>Distanz zum Reticle-Ziel in Metern (null wenn kein Hit).</summary>
    public float? HitDistanceMeters { get; init; }

    /// <summary>Höhe des Reticle-Ziels relativ zum Session-Start (null wenn kein Hit).</summary>
    public float? HitHeightDelta { get; init; }

    /// <summary>Anzahl erkannter Planes (für Stats-Panel).</summary>
    public int DetectedPlaneCount { get; init; }

    /// <summary>Session-Dauer in Sekunden.</summary>
    public long SessionSeconds { get; init; }

    /// <summary>Compass-Heading in Grad (0-360, 0=Nord, 90=Ost).</summary>
    public float CompassHeading { get; init; }

    /// <summary>Aktuelle Gesamtfläche gemessener Polygone/Konturen in m² (Live-Wert).</summary>
    public float LiveAreaSquareMeters { get; init; }

    /// <summary>Aktuelle Gesamtlänge aller Konturen in Metern.</summary>
    public float LiveLengthMeters { get; init; }

    /// <summary>Höhenbereich aller Punkte (max - min).</summary>
    public float HeightRangeMeters { get; init; }

    /// <summary>
    /// Snap-Hint: Reticle ist nah am ersten Kontur-Punkt → User kann Loop schließen.
    /// Liefert null wenn kein Auto-Close-Kandidat.
    /// </summary>
    public (float screenX, float screenY)? AutoCloseTarget { get; init; }

    /// <summary>Single-Shot Confirmation-Text (nach Undo/Redo/Punkt-Set).</summary>
    public string? TransientHint { get; init; }

    /// <summary>Anzahl aktiver Anchors (für Quality-Indikator — mehr Anchors = mehr Drift-Kompensation).</summary>
    public int AnchorCount { get; init; }

    /// <summary>Stabilitäts-Score der Kamera-Haltung (0 = bewegt, 1 = still).</summary>
    public float StabilityScore { get; init; } = 1f;

    /// <summary>Läuft gerade ein Multi-Frame-Sample? Dann Reticle "freeze" darstellen.</summary>
    public bool IsSampling { get; init; }

    /// <summary>Fortschritt der Multi-Frame-Sample-Session (0..1).</summary>
    public float SamplingProgress { get; init; }

    /// <summary>Ist der User bereit einen Punkt zu setzen? (alle Pre-Mess-Conditions erfüllt)</summary>
    public bool IsReadyToMeasure { get; init; }

    /// <summary>Kurze Check-List an fehlenden Conditions ("Kamera wackelt · Kompass unkalibriert").</summary>
    public string? ReadinessIssues { get; init; }

    /// <summary>Gesamt-Tracking-Quality 0-100 aus Tracking-Kontinuität, Planes, Stability, Mag, Anchors.</summary>
    public int TrackingQualityScore { get; init; } = 100;

    /// <summary>Y-Wert der Ground-Plane (Boden) in ARCore-Welt — null wenn noch nicht erkannt.</summary>
    public float? GroundPlaneY { get; init; }

    /// <summary>Magnetometer-Accuracy 0-3. Unter 2 → Kompass-Kalibrierung nötig.</summary>
    public int MagneticAccuracy { get; init; } = 3;

    /// <summary>Top-Inset in Pixel (Statusbar + Punch-Hole) — Overlay-Top-UI muss darunter starten.</summary>
    public float TopInsetPixels { get; init; }

    /// <summary>Bottom-Inset in Pixel (Navigation-Bar) — Overlay-Bottom-UI muss darüber enden.</summary>
    public float BottomInsetPixels { get; init; }

    /// <summary>
    /// Persistente Thermal-Warnung. null = ok, sonst Text wie "Gerät heiß — Präzision reduziert".
    /// Wird als Top-Banner unter dem Tracking-Banner gerendert, bleibt sichtbar bis Throttling aufhört.
    /// </summary>
    public string? ThermalWarning { get; init; }

    /// <summary>
    /// Persistente Battery-Warnung bei niedrigem Akku. null = ok, sonst "Akku 12%".
    /// </summary>
    public string? BatteryWarning { get; init; }

    /// <summary>Lokalisierte Labels fuer Stats-Panel + Footer + Reticle (Plan-Kap. 4.11).
    /// Default DE-Fallback, von <see cref="ArCaptureActivity"/> zu Session-Start ueberschrieben.</summary>
    public ArOverlayLabels Labels { get; init; } = ArOverlayLabels.GermanDefaults;

    /// <summary>Plan-Kap. 5.3: Tape-Measure-Punkte als Screen-Koordinaten (projiziert).
    /// Liste ist die Reihenfolge der Tap-Punkte. Wenn null oder leer: Mode nicht aktiv
    /// bzw. noch nichts gesetzt.</summary>
    public IReadOnlyList<(float screenX, float screenY)>? TapeMeasureScreenPoints { get; init; }

    /// <summary>Distanzen zwischen aufeinanderfolgenden Tape-Punkten in Metern (in
    /// Reihenfolge). Eintrag i ist die Distanz zwischen Punkt i und Punkt i+1.</summary>
    public IReadOnlyList<float>? TapeMeasureSegmentMeters { get; init; }

    /// <summary>Summe aller <see cref="TapeMeasureSegmentMeters"/> in Metern — fuer den
    /// Live-Footer im Tape-Mode statt der normalen Punkt/Flaeche/Laenge-Werte.</summary>
    public float TapeMeasureTotalMeters { get; init; }

    /// <summary>True wenn <see cref="ArCaptureActivity"/> aktuell im Tape-Measure-Mode laeuft.</summary>
    public bool IsTapeMeasureMode { get; init; }

    /// <summary>Plan-Kap. 5.9: True wenn der Stakeout-Modus aktiv ist.</summary>
    public bool IsStakeoutMode { get; init; }

    /// <summary>Distanz zum aktiven Stakeout-Target in Metern. null wenn keine Position
    /// verfuegbar oder kein Target.</summary>
    public double? StakeoutDistanceMeters { get; init; }

    /// <summary>Relative Pfeil-Richtung in Grad (0=Vorderseite Kamera/Display, im
    /// Uhrzeigersinn). Berechnet aus geografischem Bearing - aktuelles Heading. Null wenn
    /// nicht berechenbar.</summary>
    public double? StakeoutRelativeBearingDeg { get; init; }

    /// <summary>Anzeige-Label des aktuellen Targets (z.B. "Grenzpunkt 1"). null wenn alle
    /// Targets erreicht sind.</summary>
    public string? StakeoutTargetLabel { get; init; }

    /// <summary>Anzahl Targets, die in dieser Session bereits erreicht wurden — fuer
    /// Fortschritts-Anzeige.</summary>
    public int StakeoutReachedCount { get; init; }

    /// <summary>Gesamt-Anzahl Targets.</summary>
    public int StakeoutTotalCount { get; init; }
}
