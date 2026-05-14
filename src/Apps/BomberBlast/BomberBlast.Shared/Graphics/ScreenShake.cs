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

    /// <summary>
    /// Phase 21 (V4): Camera-Pull-Back-Faktor [0.85, 1.0].
    /// 1.0 = normaler Zoom, 0.95 = leichter Pull-Back, 0.85 = starker Pull-Back (Big-Hit).
    /// Der Renderer multipliziert canvas.Scale mit diesem Wert um Big-Hits visuell zu unterstreichen
    /// (God-of-War-Pattern: Camera reagiert auf Action). Sin-Kurve mit Ease-Out (peak in 1/3 Dauer).
    /// </summary>
    public float PullBackFactor { get; private set; } = 1f;

    /// <summary>Ob der Shake-Effekt aktiv ist</summary>
    public bool IsActive => _trauma > 0 || _pullBackTimer > 0;

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
    // CAMERA-PULL-BACK (Phase 21 — V4)
    // ═══════════════════════════════════════════════════════════════════════

    private float _pullBackTimer;
    private float _pullBackDuration;
    private float _pullBackMagnitude;

    /// <summary>
    /// Triggert einen Camera-Pull-Back-Effekt für Big-Hits (Boss-Kill, ULTRA-Combo).
    /// </summary>
    /// <param name="magnitude">Stärke 0..1 (0.5 = leicht, 1.0 = stark, max 15% Zoom-Out).</param>
    /// <param name="durationSeconds">Dauer der Sin-Kurve (Default 0.4s).</param>
    public void TriggerPullBack(float magnitude, float durationSeconds = 0.4f)
    {
        if (!Enabled) return;
        var clamp = Math.Clamp(magnitude, 0f, 1f);
        // Stärkster aktiver Pull-Back gewinnt — verhindert dass kurze Combo einen Big-Hit-Pull überschreibt
        if (clamp * durationSeconds > _pullBackMagnitude * _pullBackDuration)
        {
            _pullBackMagnitude = clamp;
            _pullBackDuration = durationSeconds;
            _pullBackTimer = durationSeconds;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // UPDATE
    // ═══════════════════════════════════════════════════════════════════════

    public void Update(float deltaTime)
    {
        // Camera-Pull-Back-Hüllkurve (Phase 21): Sin-Kurve mit Peak bei 33% Lifetime
        if (_pullBackTimer > 0f)
        {
            _pullBackTimer = MathF.Max(0f, _pullBackTimer - deltaTime);
            float progress = _pullBackDuration > 0f ? 1f - (_pullBackTimer / _pullBackDuration) : 1f;
            // Smoothstep-In + Linear-Out: schneller Peak, langsame Recovery
            float curve = progress < 0.33f
                ? (progress / 0.33f) * (progress / 0.33f) * (3f - 2f * (progress / 0.33f))  // Smoothstep-In
                : 1f - ((progress - 0.33f) / 0.67f);                                          // Linear-Out
            curve = Math.Clamp(curve, 0f, 1f);
            // Max 15% Zoom-Out bei magnitude=1.0
            PullBackFactor = 1f - (0.15f * _pullBackMagnitude * curve);
        }
        else
        {
            PullBackFactor = 1f;
            _pullBackMagnitude = 0f;
        }

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
        _pullBackTimer = 0;
        _pullBackDuration = 0;
        _pullBackMagnitude = 0;
        PullBackFactor = 1f;
    }
}
