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

    // ═══════════════════════════════════════════════════════════════════════
    // v2.0.45 — Haptic-Library (12 Pattern-Definitionen, AAA-Audit P1)
    // ═══════════════════════════════════════════════════════════════════════
    // Default-Implementations delegieren an die 4 Basis-Methoden, sodass
    // existing Implementations (NullVibrationService) automatisch greifen.

    /// <summary>Bombe gelegt: Subtiler Doppel-Tick (10ms + 10ms Pause + 10ms).</summary>
    void VibrateBombPlant() => VibrateLight();

    /// <summary>Spezial-Bomben-Aktivierung: Stärkerer Triple-Tap (15+15+15ms).</summary>
    void VibrateSpecialBomb() => VibrateMedium();

    /// <summary>PowerUp eingesammelt: Knackiger Single-Pulse (20ms steigender Sweep).</summary>
    void VibratePickUp() => VibrateLight();

    /// <summary>Shield-Treffer (Spieler überlebt): Zwei kurze starke Pulse.</summary>
    void VibrateShieldHit() => VibrateMedium();

    /// <summary>Spieler-Tod: Langer abklingender Pulse (200ms abnehmende Amplitude).</summary>
    void VibrateDeath() => VibrateHeavy();

    /// <summary>Level-Complete-Fanfare: 3 ansteigende Pulse (60-90-120ms).</summary>
    void VibrateLevelComplete() => VibratePattern();

    /// <summary>Boss-Roar / Boss-Spawn: Tiefes Grollen (400ms voller Amplitude).</summary>
    void VibrateBossRoar() => VibrateHeavy();

    /// <summary>Curse-Pickup (Skull): Schaudernder Doppel-Pulse (30+30 mit Pause).</summary>
    void VibrateCurse() => VibrateMedium();

    /// <summary>Combo x5+: Kaskadierender Tick-Strom (4× Tick-Pulse).</summary>
    void VibrateCombo() => VibrateLight();

    /// <summary>Achievement-Unlock: Erfolgs-Pattern (kurz-kurz-lang).</summary>
    void VibrateAchievement() => VibratePattern();

    /// <summary>Ob Vibration aktiviert ist (Benutzer-Einstellung)</summary>
    bool IsEnabled { get; set; }
}
