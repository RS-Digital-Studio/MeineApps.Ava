namespace BomberBlast.Graphics;

/// <summary>
/// Screen-Shake-Effekt für Explosionen und Spieler-Tod.
/// Erzeugt zufällige Verschiebung die exponentiell abklingt.
/// </summary>
public sealed class ScreenShake
{
    private float _intensity;
    private float _duration;
    private float _timer;
    private readonly Random _random = new();

    /// <summary>Aktuelle horizontale Verschiebung in Pixeln</summary>
    public float OffsetX { get; private set; }

    /// <summary>Aktuelle vertikale Verschiebung in Pixeln</summary>
    public float OffsetY { get; private set; }

    /// <summary>Ob der Shake-Effekt aktiv ist</summary>
    public bool IsActive => _timer > 0;

    /// <summary>Wenn false, werden Trigger-Aufrufe ignoriert (ReducedEffects)</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Shake auslösen
    /// </summary>
    /// <param name="intensity">Maximale Verschiebung in Pixeln</param>
    /// <param name="duration">Dauer in Sekunden</param>
    public void Trigger(float intensity, float duration)
    {
        if (!Enabled) return;
        // Nur überschreiben wenn stärker als aktueller Shake
        // _duration == 0 beim ersten Aufruf → Division by Zero vermeiden
        if (_duration <= 0 || intensity > _intensity * (_timer / _duration))
        {
            _intensity = intensity;
            _duration = duration;
            _timer = duration;
        }
    }

    /// <summary>
    /// Shake aktualisieren
    /// </summary>
    public void Update(float deltaTime)
    {
        if (_timer <= 0)
        {
            OffsetX = 0;
            OffsetY = 0;
            return;
        }

        _timer -= deltaTime;
        if (_timer <= 0)
        {
            _timer = 0;
            OffsetX = 0;
            OffsetY = 0;
            return;
        }

        // Exponentielles Abklingen
        float progress = _timer / _duration;
        float currentIntensity = _intensity * progress * progress;

        // Zufällige Richtung
        OffsetX = ((float)_random.NextDouble() * 2f - 1f) * currentIntensity;
        OffsetY = ((float)_random.NextDouble() * 2f - 1f) * currentIntensity;
    }

    /// <summary>Shake sofort beenden</summary>
    public void Reset()
    {
        _timer = 0;
        OffsetX = 0;
        OffsetY = 0;
    }
}
