namespace RebornSaga.Engine.Transitions;

using SkiaSharp;
using System;

/// <summary>
/// Basis für Szenen-Übergänge. Rendert beide Szenen während des Übergangs.
/// Progress geht von 0 (Start) bis 1 (Ende).
/// </summary>
public abstract class TransitionEffect
{
    /// <summary>
    /// Dauer des Übergangs in Sekunden.
    /// </summary>
    public float Duration { get; protected set; }

    /// <summary>
    /// Aktueller Fortschritt (0..1).
    /// </summary>
    public float Progress { get; protected set; }

    /// <summary>
    /// Gibt an, ob der Übergang abgeschlossen ist.
    /// </summary>
    public bool IsComplete => Progress >= 1f;

    protected TransitionEffect(float durationSeconds)
    {
        Duration = durationSeconds;
    }

    /// <summary>
    /// Fortschritt aktualisieren. Wird vom SceneManager pro Frame aufgerufen.
    /// </summary>
    public void Update(float deltaTime)
    {
        // Guard: Bei ungültiger Duration sofort abschließen (verhindert Division by Zero)
        if (Duration <= 0f)
        {
            Progress = 1f;
            return;
        }

        Progress = Math.Min(1f, Progress + deltaTime / Duration);
    }

    /// <summary>
    /// Progress zurücksetzen für Wiederverwendung.
    /// </summary>
    public void Reset()
    {
        Progress = 0f;
    }

    /// <summary>
    /// Rendert den Übergang. Die Callbacks zeichnen die jeweilige Szene.
    /// </summary>
    /// <param name="canvas">SkiaSharp-Canvas.</param>
    /// <param name="bounds">Sichtbarer Bereich.</param>
    /// <param name="renderOldScene">Zeichnet die alte Szene.</param>
    /// <param name="renderNewScene">Zeichnet die neue Szene.</param>
    public abstract void Render(SKCanvas canvas, SKRect bounds,
        Action<SKCanvas, SKRect> renderOldScene,
        Action<SKCanvas, SKRect> renderNewScene);

    /// <summary>
    /// Easing-Funktion (Ease-In-Out Cubic) für smoothe Übergänge.
    /// </summary>
    protected float Ease(float t)
    {
        return t < 0.5f
            ? 4f * t * t * t
            : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;
    }
}
