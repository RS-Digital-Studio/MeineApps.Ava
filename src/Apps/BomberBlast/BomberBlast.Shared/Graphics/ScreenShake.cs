namespace BomberBlast.Graphics;

/// <summary>
/// Screen-Shake-Effekt mit Trauma-Decay-Modell (Squirrel Eiserloh, "Math for Game Programmers").
/// Trauma akkumuliert sich (mehrere Explosionen → stärkerer Shake), klingt linear ab.
/// Shake-Amplitude = baseAmplitude * trauma^2 (quadratisch, kleine Trauma-Werte fast nicht spürbar).
/// Distanz-Skalierung über TriggerAt(intensity, duration, distance) für räumliches Feedback.
/// </summary>
public sealed class ScreenShake
{
    // ═══════════════════════════════════════════════════════════════════════
    // TRAUMA-MODELL (primärer Pfad)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Akkumulierter Trauma-Wert [0, 1]. Erzeugt Shake = base * trauma^2.</summary>
    private float _trauma;

    /// <summary>Trauma-Decay pro Sekunde (linear). 1.5 = klingt in 0.67s vollständig ab.</summary>
    private const float TraumaDecay = 1.5f;

    /// <summary>Maximale Pixel-Amplitude bei trauma=1.0.</summary>
    private const float MaxTraumaAmplitude = 12f;

    /// <summary>Maximale Rotations-Amplitude in Grad bei trauma=1.0 (subtil).</summary>
    private const float MaxTraumaRotation = 1.5f;

    private readonly Random _random = new();

    /// <summary>Aktuelle horizontale Verschiebung in Pixeln</summary>
    public float OffsetX { get; private set; }

    /// <summary>Aktuelle vertikale Verschiebung in Pixeln</summary>
    public float OffsetY { get; private set; }

    /// <summary>Aktuelle Rotation in Grad (subtil, für GameRenderer-Anwendung)</summary>
    public float RotationDegrees { get; private set; }

    /// <summary>Ob der Shake-Effekt aktiv ist</summary>
    public bool IsActive => _trauma > 0;

    /// <summary>Wenn false, werden Trigger-Aufrufe ignoriert (ReducedEffects/Accessibility)</summary>
    public bool Enabled { get; set; } = true;

    // ═══════════════════════════════════════════════════════════════════════
    // TRIGGER (Legacy + Trauma)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Legacy-Pfad für Bestand-Aufrufer (Player-Death, Game-Over).
    /// Konvertiert intensity (Pixel) in Trauma-Anteil.
    /// </summary>
    /// <param name="intensity">Maximale Verschiebung in Pixeln (1-10 typisch)</param>
    /// <param name="duration">Dauer in Sekunden (informativ, wird als Trauma-Anteil gemapped)</param>
    public void Trigger(float intensity, float duration)
    {
        if (!Enabled) return;
        // intensity in Trauma-Anteil mappen: 5px ≈ 0.5 Trauma, 10px ≈ 1.0 Trauma
        float traumaAdd = MathF.Min(1f, intensity / 10f);
        // Längere Duration → mehr Trauma (max 0.3 Bonus)
        traumaAdd += MathF.Min(0.3f, duration * 0.5f);
        AddTrauma(traumaAdd);
    }

    /// <summary>
    /// Trauma-basierter Trigger mit Distanz-Skalierung.
    /// distanceCells: Manhattan-Distanz zum Spieler in Grid-Zellen (0 = direkt am Spieler).
    /// Trauma-Beitrag wird mit 1/(1+distance/falloff) skaliert.
    /// </summary>
    public void TriggerAt(float baseAmount, float distanceCells, float falloffCells = 4f)
    {
        if (!Enabled) return;
        float distanceMul = 1f / (1f + MathF.Max(0f, distanceCells) / MathF.Max(0.5f, falloffCells));
        AddTrauma(baseAmount * distanceMul);
    }

    /// <summary>Trauma direkt addieren (clamped auf [0, 1]).</summary>
    public void AddTrauma(float amount)
    {
        if (!Enabled || amount <= 0) return;
        _trauma = MathF.Min(1f, _trauma + amount);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // UPDATE
    // ═══════════════════════════════════════════════════════════════════════

    public void Update(float deltaTime)
    {
        if (_trauma <= 0)
        {
            OffsetX = 0;
            OffsetY = 0;
            RotationDegrees = 0;
            return;
        }

        // Linearer Trauma-Decay
        _trauma = MathF.Max(0f, _trauma - TraumaDecay * deltaTime);

        // Shake = base * trauma^2 (quadratisch — kleine Trauma-Werte spürbar zurückgenommen)
        float shake = _trauma * _trauma;
        float amplitude = MaxTraumaAmplitude * shake;
        float rotation = MaxTraumaRotation * shake;

        // Zufällige Richtung pro Frame (klassisches Pattern)
        OffsetX = ((float)_random.NextDouble() * 2f - 1f) * amplitude;
        OffsetY = ((float)_random.NextDouble() * 2f - 1f) * amplitude;
        RotationDegrees = ((float)_random.NextDouble() * 2f - 1f) * rotation;
    }

    /// <summary>Shake sofort beenden</summary>
    public void Reset()
    {
        _trauma = 0;
        OffsetX = 0;
        OffsetY = 0;
        RotationDegrees = 0;
    }
}
