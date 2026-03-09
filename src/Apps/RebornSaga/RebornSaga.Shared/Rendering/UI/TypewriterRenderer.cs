namespace RebornSaga.Rendering.UI;

using System;

/// <summary>
/// Typewriter-Effekt: Zeigt Text Buchstabe für Buchstabe an.
/// Einstellbare Geschwindigkeit, Skip zum sofortigen Anzeigen.
/// </summary>
public class TypewriterRenderer
{
    private string _fullText = "";
    private float _charIndex;
    private float _speed = 30f; // Zeichen/Sekunde

    /// <summary>Ist der gesamte Text sichtbar?</summary>
    public bool IsComplete => _charIndex >= _fullText.Length;

    /// <summary>Aktuell sichtbarer Text (Substring bis zum aktuellen Zeichen).</summary>
    public string VisibleText => _fullText.Length > 0
        ? _fullText[..Math.Min((int)_charIndex, _fullText.Length)]
        : "";

    /// <summary>Vollständiger Text.</summary>
    public string FullText => _fullText;

    /// <summary>
    /// Setzt einen neuen Text und startet den Typewriter-Effekt von vorne.
    /// </summary>
    public void SetText(string text)
    {
        _fullText = text ?? "";
        _charIndex = 0;
    }

    /// <summary>Zeigt den gesamten Text sofort an (Skip).</summary>
    public void ShowAll()
    {
        _charIndex = _fullText.Length;
    }

    /// <summary>
    /// Aktualisiert den Typewriter (erhöht den sichtbaren Zeichenindex).
    /// </summary>
    public void Update(float deltaTime)
    {
        if (!IsComplete)
            _charIndex += _speed * deltaTime;
    }

    /// <summary>
    /// Setzt die Schreibgeschwindigkeit.
    /// </summary>
    public void SetSpeed(TypewriterSpeed speed) => _speed = speed switch
    {
        TypewriterSpeed.Slow => 15f,
        TypewriterSpeed.Medium => 30f,
        TypewriterSpeed.Fast => 60f,
        TypewriterSpeed.Instant => 9999f,
        _ => 30f
    };

    /// <summary>
    /// Setzt eine benutzerdefinierte Geschwindigkeit (Zeichen/Sekunde).
    /// </summary>
    public void SetSpeed(float charsPerSecond)
    {
        _speed = Math.Max(1f, charsPerSecond);
    }
}

/// <summary>
/// Voreingestellte Typewriter-Geschwindigkeiten.
/// </summary>
public enum TypewriterSpeed
{
    Slow,
    Medium,
    Fast,
    Instant
}
