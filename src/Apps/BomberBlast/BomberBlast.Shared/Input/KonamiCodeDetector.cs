using BomberBlast.Models.Entities;

namespace BomberBlast.Input;

/// <summary>
/// Konami-Code-Detector (Phase 28 — AAA-Audit PR5).
///
/// <para>Klassisches Easter-Egg: Spieler-Input <c>Up Up Down Down Left Right Left Right Bomb Detonate</c>
/// (statt B+A wie im Original) löst einen Bonus-Effekt aus. Funktioniert auf allen Input-Methoden
/// (Keyboard, Joystick, Gamepad).</para>
///
/// <para>Anti-Spam: 1× pro Session (boolean-Flag). Sichtbares Feedback nur via Floating-Text +
/// Konfetti-Explosion. Keine permanenten Auswirkungen — Robert kann das später mit
/// "+999 Coins one-time" oder "alle Cosmetics für 24h gratis" verkabeln, falls gewünscht.</para>
/// </summary>
public sealed class KonamiCodeDetector
{
    /// <summary>Erwartete Sequenz (10 Schritte).</summary>
    public enum InputStep
    {
        Up,
        Down,
        Left,
        Right,
        Bomb,
        Detonate,
    }

    private static readonly InputStep[] Sequence =
    [
        InputStep.Up, InputStep.Up,
        InputStep.Down, InputStep.Down,
        InputStep.Left, InputStep.Right,
        InputStep.Left, InputStep.Right,
        InputStep.Bomb, InputStep.Detonate,
    ];

    private int _currentStep;
    private float _timeSinceLastInput;
    private const float Timeout = 3.0f; // Sekunden zwischen Schritten — sonst Reset

    /// <summary>True wenn der Code in dieser Session bereits ausgelöst wurde (1× pro Session-Limit).</summary>
    public bool HasBeenTriggered { get; private set; }

    /// <summary>Wird gefeuert wenn der Code vollständig erkannt wurde.</summary>
    public event Action? CodeTriggered;

    /// <summary>
    /// Pro Frame im InputManager-Update aufrufen, danach <see cref="RegisterInput"/> bei jedem
    /// neuen Input-Event.
    /// </summary>
    public void Update(float deltaTime)
    {
        if (_currentStep > 0)
        {
            _timeSinceLastInput += deltaTime;
            if (_timeSinceLastInput > Timeout)
            {
                Reset();
            }
        }
    }

    /// <summary>
    /// Registriert einen einzelnen Input-Schritt. Wenn er zur erwarteten Sequenz passt:
    /// Schritt um 1 erhöhen. Sonst: Reset (falls erster Schritt der Sequenz, dort beginnen).
    /// </summary>
    public void RegisterInput(InputStep step)
    {
        if (HasBeenTriggered) return;

        _timeSinceLastInput = 0f;

        if (Sequence[_currentStep] == step)
        {
            _currentStep++;
            if (_currentStep >= Sequence.Length)
            {
                HasBeenTriggered = true;
                CodeTriggered?.Invoke();
                _currentStep = 0;
            }
        }
        else
        {
            // Wenn der falsche Schritt zufällig der erste Schritt der Sequenz ist,
            // setze _currentStep auf 1 (statt 0) damit der Spieler bei "Up Up" nicht von vorn beginnen muss.
            _currentStep = (Sequence[0] == step) ? 1 : 0;
        }
    }

    /// <summary>
    /// Mappt eine <see cref="Direction"/> auf den korrespondierenden InputStep.
    /// Liefert null für Direction.None oder Diagonale (gibt es nicht in 4-Wege-Bomberman).
    /// </summary>
    public static InputStep? FromDirection(Direction dir) => dir switch
    {
        Direction.Up => InputStep.Up,
        Direction.Down => InputStep.Down,
        Direction.Left => InputStep.Left,
        Direction.Right => InputStep.Right,
        _ => null,
    };

    /// <summary>Setzt den State zurück (z.B. bei Level-Wechsel oder explizit per Test).</summary>
    public void Reset()
    {
        _currentStep = 0;
        _timeSinceLastInput = 0f;
    }

    /// <summary>Wird beim App-Start aufgerufen — setzt auch HasBeenTriggered zurück (Test-Hook).</summary>
    internal void ResetForTesting()
    {
        Reset();
        HasBeenTriggered = false;
    }

    /// <summary>Aktueller Fortschritt 0..1 (für UI-Hint, falls sichtbar gemacht).</summary>
    public float Progress => _currentStep / (float)Sequence.Length;
}
