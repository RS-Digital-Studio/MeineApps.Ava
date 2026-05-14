namespace BomberBlast.Core;

/// <summary>
/// Fixed-Timestep-Foundation (v2.0.54 — Phase 13).
///
/// Implementiert das klassische Akkumulator-Pattern für deterministische Sim-Ticks:
/// - Wall-Clock-DeltaTime wird im Akkumulator gesammelt
/// - Solange Akkumulator >= FIXED_TICK_SECONDS: Sim-Tick ausführen (deterministische 60 Hz)
/// - Render-Interpolation-Alpha wird zwischen Ticks berechnet
///
/// AKTUELLER STAND: Foundation-Klasse ist fertig + getestet, aber NICHT in GameEngine.Update integriert.
/// Variable-Timestep bleibt der Default — Fixed-Mode ist ein opt-in Future-Feature.
///
/// Voraussetzung für: Replay-System, Anti-Cheat-Server-Validation, Async-PvP
/// (alle benötigen deterministische Frame-für-Frame-Reproduzierbarkeit).
///
/// Migration in 4 Schritten geplant (eigener Sprint nach Live-Verifikation):
/// 1. GameEngine.Update zerlegen: Sim-Logic vs. Render-Logic
/// 2. Sim-Logic in einer for-loop pro Sim-Tick aufrufen
/// 3. Render-Logic mit Interpolation-Alpha versorgen
/// 4. Random-Calls auf seed-deterministisch umstellen
/// </summary>
public sealed class FixedTimestepRunner
{
    /// <summary>Sim-Tick-Rate in Hz. 60 ist Industry-Standard.</summary>
    public const int FIXED_HZ = 60;

    /// <summary>Sim-Tick-Dauer in Sekunden (1/60).</summary>
    public const float FIXED_TICK_SECONDS = 1f / FIXED_HZ;

    /// <summary>Spiral-of-Death-Schutz: max 5 Ticks pro Frame
    /// (wenn Wall-Clock-Frame zu lang dauert, wird der Akkumulator gekappt).</summary>
    public const int MAX_TICKS_PER_FRAME = 5;

    private float _accumulator;

    /// <summary>
    /// User-Toggle für Fixed-Mode. Default off (Variable-Timestep bleibt der Live-Mode).
    /// In Settings exposed als Pref-Key "FixedTimestepEnabled".
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Akkumulator-Wert in Sekunden (für Tests/Debugging).
    /// </summary>
    public float Accumulator => _accumulator;

    /// <summary>
    /// Anzahl Sim-Ticks die für diesen Frame ausgeführt werden sollen.
    /// Im Variable-Mode (Enabled=false) liefert die Methode 0 + füllt den Akkumulator nicht
    /// (Engine bleibt bei Variable-Timestep-Logic).
    /// </summary>
    public int GetTicksForFrame(float wallDeltaTime)
    {
        if (!Enabled) return 0;

        _accumulator += wallDeltaTime;

        int ticks = 0;
        while (_accumulator >= FIXED_TICK_SECONDS && ticks < MAX_TICKS_PER_FRAME)
        {
            _accumulator -= FIXED_TICK_SECONDS;
            ticks++;
        }

        // Spiral-of-Death-Schutz: bei Frame-Drop akkumuliert sich der Wall-Clock-Lag.
        // Wenn wir die MAX_TICKS_PER_FRAME-Grenze hitten, kappen wir den Akkumulator
        // statt ihn weiter wachsen zu lassen — sonst Cascade.
        if (_accumulator > FIXED_TICK_SECONDS * MAX_TICKS_PER_FRAME)
            _accumulator = FIXED_TICK_SECONDS * MAX_TICKS_PER_FRAME;

        return ticks;
    }

    /// <summary>
    /// Render-Interpolation-Alpha [0,1].
    /// 0 = Render exakt am letzten Sim-Tick, 1 = Render kurz vor nächstem Sim-Tick.
    /// Für visuelle Glättung zwischen Ticks (lerp zwischen previousState und currentState).
    /// </summary>
    public float GetInterpolationAlpha() =>
        _accumulator / FIXED_TICK_SECONDS;

    /// <summary>
    /// Akkumulator zurücksetzen (z.B. bei Pause/Resume oder Mode-Wechsel).
    /// Verhindert Catch-Up-Cascade nach langem Pause-State.
    /// </summary>
    public void Reset() => _accumulator = 0;
}
