namespace BomberBlast.Services;

/// <summary>
/// Haptisches Feedback für Gameplay-Events.
/// Android: Vibrator-API, Desktop: NullVibrationService.
/// </summary>
public interface IVibrationService
{
    /// <summary>Sehr kurzes Tick-Feedback (15ms — Joystick-Richtungswechsel, Pre-Turn-Buffering)</summary>
    void VibrateTick();

    /// <summary>Leichtes haptisches Feedback (PowerUp einsammeln, UI-Tap)</summary>
    void VibrateLight();

    /// <summary>Mittleres haptisches Feedback (Explosion, Shield-Absorption, Boss-Hit)</summary>
    void VibrateMedium();

    /// <summary>Starkes haptisches Feedback (Spieler-Tod)</summary>
    void VibrateHeavy();

    /// <summary>Muster-Vibration (Level-Complete, Boss besiegt)</summary>
    void VibratePattern();

    /// <summary>Ob Vibration aktiviert ist (Benutzer-Einstellung)</summary>
    bool IsEnabled { get; set; }
}
