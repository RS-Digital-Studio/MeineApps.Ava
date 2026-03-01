namespace MeineApps.UI.SkiaSharp.SplashScreen;

/// <summary>
/// Struct für schwebende Glow-Partikel im Splash-Screen.
/// Fixed-Size Pool (kein GC-Pressure) mit Sinus-basiertem Floating.
/// </summary>
public struct SplashParticle
{
    /// <summary>Position X (0-1 normalisiert auf Canvas-Breite)</summary>
    public float X;

    /// <summary>Position Y (0-1 normalisiert auf Canvas-Höhe)</summary>
    public float Y;

    /// <summary>Radius in dp (2-6)</summary>
    public float Radius;

    /// <summary>Alpha-Wert (40-120)</summary>
    public float Alpha;

    /// <summary>Sinus-Phase für Floating-Animation (0 - 2*PI)</summary>
    public float Phase;

    /// <summary>Horizontale Drift-Geschwindigkeit (-0.02 bis 0.02)</summary>
    public float SpeedX;

    /// <summary>Vertikale Drift-Geschwindigkeit (-0.01 bis 0.01)</summary>
    public float SpeedY;

    /// <summary>Amplitude der Sinus-Schwingung (5-20 dp)</summary>
    public float FloatAmplitude;

    /// <summary>Frequenz der Sinus-Schwingung (0.5-2.0 Hz)</summary>
    public float FloatFrequency;

    /// <summary>Basis-X für Sinus-Oszillation (wird bei Init gesetzt)</summary>
    public float BaseX;
}
