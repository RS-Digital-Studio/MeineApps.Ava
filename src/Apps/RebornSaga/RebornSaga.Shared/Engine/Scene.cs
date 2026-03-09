namespace RebornSaga.Engine;

using SkiaSharp;

/// <summary>
/// Basisklasse für alle Spielszenen (Dialog, Kampf, Overworld, etc.)
/// Jede Szene hat einen vollständigen Lifecycle und wird vom SceneManager verwaltet.
/// </summary>
public abstract class Scene
{
    /// <summary>
    /// Referenz auf den SceneManager (für Szenen-Wechsel aus der Szene heraus).
    /// </summary>
    public SceneManager SceneManager { get; internal set; } = null!;

    /// <summary>
    /// Gibt an, ob die Szene gerade aktiv ist (im Stack oder als Overlay).
    /// </summary>
    public bool IsActive { get; internal set; }

    // --- Lifecycle (wird vom SceneManager aufgerufen) ---

    /// <summary>
    /// Wird aufgerufen wenn die Szene betreten wird. Ressourcen laden, Animationen starten.
    /// </summary>
    public virtual void OnEnter() { }

    /// <summary>
    /// Wird aufgerufen wenn die Szene verlassen wird. Cleanup, Ressourcen freigeben.
    /// </summary>
    public virtual void OnExit() { }

    /// <summary>
    /// Wird aufgerufen wenn eine neue Szene über diese gepusht wird (Pause-Zustand).
    /// </summary>
    public virtual void OnPause() { }

    /// <summary>
    /// Wird aufgerufen wenn die obere Szene entfernt wird und diese wieder aktiv ist.
    /// </summary>
    public virtual void OnResume() { }

    // --- Game Loop (60fps) ---

    /// <summary>
    /// Spiellogik pro Frame aktualisieren.
    /// </summary>
    /// <param name="deltaTime">Vergangene Zeit seit letztem Frame in Sekunden.</param>
    public abstract void Update(float deltaTime);

    /// <summary>
    /// Szene auf das SkiaSharp-Canvas zeichnen.
    /// </summary>
    /// <param name="canvas">SkiaSharp-Canvas zum Zeichnen.</param>
    /// <param name="bounds">Sichtbarer Bereich (canvas.LocalClipBounds).</param>
    public abstract void Render(SKCanvas canvas, SKRect bounds);

    // --- Input (abstrahierte Aktionen) ---

    /// <summary>
    /// Gibt an, ob dieses Overlay Input konsumiert (true) oder an die darunterliegende Szene durchreicht (false).
    /// Standard: true (Overlays blockieren Input). Für transparente Overlays wie EffectFeedback auf false setzen.
    /// </summary>
    public virtual bool ConsumesInput => true;

    /// <summary>
    /// Verarbeitet eine abstrahierte Eingabe-Aktion (Tap, Swipe, Hold, etc.).
    /// </summary>
    public virtual void HandleInput(InputAction action, SKPoint position) { }

    // --- Rohe Pointer-Events (für Drag, Hover, Buttons etc.) ---

    /// <summary>
    /// Touch/Maus gedrückt.
    /// </summary>
    public virtual void HandlePointerDown(SKPoint position) { }

    /// <summary>
    /// Touch/Maus bewegt.
    /// </summary>
    public virtual void HandlePointerMove(SKPoint position) { }

    /// <summary>
    /// Touch/Maus losgelassen.
    /// </summary>
    public virtual void HandlePointerUp(SKPoint position) { }
}
