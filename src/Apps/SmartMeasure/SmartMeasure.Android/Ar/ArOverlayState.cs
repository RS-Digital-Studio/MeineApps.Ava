namespace SmartMeasure.Android.Ar;

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
}
