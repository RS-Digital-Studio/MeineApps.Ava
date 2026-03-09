namespace RebornSaga.Engine;

using Microsoft.Extensions.DependencyInjection;
using RebornSaga.Engine.Transitions;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Herzstück der Engine. Verwaltet den Szenen-Stack, Overlays und Übergänge.
/// Szenen werden per DI erstellt (Constructor Injection möglich).
/// </summary>
public class SceneManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Stack<Scene> _sceneStack = new();
    private readonly List<Scene> _overlays = new();

    // Transition-State
    private TransitionEffect? _activeTransition;
    private Scene? _oldScene;
    private Scene? _newScene;
    private Action? _onTransitionComplete;

    /// <summary>
    /// Die aktuell aktive Szene (oberste im Stack).
    /// </summary>
    public Scene? CurrentScene => _sceneStack.Count > 0 ? _sceneStack.Peek() : null;

    /// <summary>
    /// Aktive Overlays (werden über allen Szenen gerendert).
    /// </summary>
    public IReadOnlyList<Scene> Overlays => _overlays;

    /// <summary>
    /// Gibt an, ob gerade ein Übergang läuft.
    /// </summary>
    public bool IsTransitioning => _activeTransition != null;

    /// <summary>
    /// Gibt an, ob unter der aktuellen Szene noch eine weitere liegt (PushScene-Stack).
    /// </summary>
    public bool HasSceneBelow => _sceneStack.Count > 1;

    public SceneManager(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Ersetzt die aktive Szene mit Konfiguration vor OnEnter.
    /// </summary>
    public void ChangeScene<T>(Action<T> configure, TransitionEffect? transition = null) where T : Scene
    {
        ChangeSceneInternal<T>(configure, transition);
    }

    /// <summary>
    /// Ersetzt die aktive Szene (alte wird entfernt, OnExit aufgerufen).
    /// Optional mit Übergangs-Effekt.
    /// </summary>
    public void ChangeScene<T>(TransitionEffect? transition = null) where T : Scene
    {
        ChangeSceneInternal<T>(null, transition);
    }

    /// <summary>
    /// Interne Implementierung für Szenenwechsel mit optionaler Konfiguration.
    /// configure wird nach Erstellung aber VOR OnEnter aufgerufen.
    /// </summary>
    private void ChangeSceneInternal<T>(Action<T>? configure, TransitionEffect? transition) where T : Scene
    {
        // Re-Entrancy-Guard: Kein Szenenwechsel während laufender Transition
        if (IsTransitioning) return;

        var newScene = CreateScene<T>();
        configure?.Invoke(newScene);

        if (transition != null && CurrentScene != null)
        {
            // Neue Szene VOR der Transition initialisieren, damit sie im
            // Transition-Render bereits gezeichnet werden kann (OnEnter lädt
            // Ressourcen, setzt Anfangszustand etc.)
            newScene.SceneManager = this;
            newScene.OnEnter();

            StartTransition(transition, CurrentScene, newScene, () =>
            {
                // Alte Szene entfernen und disposen
                if (_sceneStack.Count > 0)
                {
                    var old = _sceneStack.Pop();
                    old.IsActive = false;
                    old.OnExit();
                    if (old is IDisposable disposable)
                        disposable.Dispose();
                }

                // Neue Szene aktivieren (OnEnter wurde bereits vor der Transition aufgerufen)
                newScene.IsActive = true;
                _sceneStack.Push(newScene);
            });
        }
        else
        {
            // Sofortiger Wechsel ohne Transition
            if (_sceneStack.Count > 0)
            {
                var old = _sceneStack.Pop();
                old.IsActive = false;
                old.OnExit();
                if (old is IDisposable disposable)
                    disposable.Dispose();
            }

            newScene.SceneManager = this;
            newScene.IsActive = true;
            _sceneStack.Push(newScene);
            newScene.OnEnter();
        }
    }

    /// <summary>
    /// Legt eine Szene über die aktuelle (z.B. Inventar über Overworld).
    /// Die untere Szene wird pausiert (OnPause), nicht entfernt.
    /// </summary>
    public void PushScene<T>(TransitionEffect? transition = null) where T : Scene
    {
        // Re-Entrancy-Guard: Kein Push während laufender Transition
        if (IsTransitioning) return;

        var newScene = CreateScene<T>();

        if (transition != null && CurrentScene != null)
        {
            // Neue Szene VOR der Transition initialisieren (wie bei ChangeScene)
            newScene.SceneManager = this;
            newScene.OnEnter();

            StartTransition(transition, CurrentScene, newScene, () =>
            {
                CurrentScene?.OnPause();

                // Neue Szene aktivieren (OnEnter wurde bereits vor der Transition aufgerufen)
                newScene.IsActive = true;
                _sceneStack.Push(newScene);
            });
        }
        else
        {
            CurrentScene?.OnPause();
            newScene.SceneManager = this;
            newScene.IsActive = true;
            _sceneStack.Push(newScene);
            newScene.OnEnter();
        }
    }

    /// <summary>
    /// Entfernt die obere Szene und kehrt zur darunter liegenden zurück.
    /// Die untere Szene wird fortgesetzt (OnResume).
    /// </summary>
    public void PopScene(TransitionEffect? transition = null)
    {
        // Re-Entrancy-Guard: Kein Pop während laufender Transition
        if (IsTransitioning) return;
        if (_sceneStack.Count <= 1) return;

        var oldScene = _sceneStack.Peek();

        // Die Szene darunter peeken (Stack.ToArray() gibt oben→unten zurück)
        var scenes = _sceneStack.ToArray();
        var nextScene = scenes.Length > 1 ? scenes[1] : null;

        if (transition != null && nextScene != null)
        {
            StartTransition(transition, oldScene, nextScene, () =>
            {
                var popped = _sceneStack.Pop();
                popped.IsActive = false;
                popped.OnExit();
                if (popped is IDisposable disposable)
                    disposable.Dispose();
                CurrentScene?.OnResume();
            });
        }
        else
        {
            var popped = _sceneStack.Pop();
            popped.IsActive = false;
            popped.OnExit();
            if (popped is IDisposable disposable)
                disposable.Dispose();
            CurrentScene?.OnResume();
        }
    }

    /// <summary>
    /// Zeigt ein transparentes Overlay über allen Szenen (z.B. Schadens-Zahlen, Toast-Nachrichten).
    /// </summary>
    public T ShowOverlay<T>() where T : Scene
    {
        var overlay = CreateScene<T>();
        overlay.SceneManager = this;
        overlay.IsActive = true;
        _overlays.Add(overlay);
        overlay.OnEnter();
        return overlay;
    }

    /// <summary>
    /// Entfernt das erste Overlay eines bestimmten Typs.
    /// </summary>
    public void HideOverlay<T>() where T : Scene
    {
        var overlay = _overlays.OfType<T>().FirstOrDefault();
        if (overlay != null)
        {
            overlay.IsActive = false;
            overlay.OnExit();
            _overlays.Remove(overlay);
            if (overlay is IDisposable disposable)
                disposable.Dispose();
        }
    }

    /// <summary>
    /// Entfernt ein spezifisches Overlay (z.B. per Referenz aus ShowOverlay).
    /// </summary>
    public void HideOverlay(Scene overlay)
    {
        if (_overlays.Remove(overlay))
        {
            overlay.IsActive = false;
            overlay.OnExit();
            if (overlay is IDisposable disposable)
                disposable.Dispose();
        }
    }

    /// <summary>
    /// Update-Tick: Transition oder aktive Szene + Overlays aktualisieren.
    /// </summary>
    public void Update(float deltaTime)
    {
        // Transition hat Vorrang
        if (_activeTransition != null)
        {
            _activeTransition.Update(deltaTime);
            if (_activeTransition.IsComplete)
            {
                _onTransitionComplete?.Invoke();
                _activeTransition = null;
                _oldScene = null;
                _newScene = null;
                _onTransitionComplete = null;
            }
            return; // Während Transition kein Szenen-Update
        }

        // Aktive Szene updaten
        CurrentScene?.Update(deltaTime);

        // Overlays updaten
        for (int i = 0; i < _overlays.Count; i++)
            _overlays[i].Update(deltaTime);
    }

    /// <summary>
    /// Render-Tick: Transition oder aktive Szene + Overlays zeichnen.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds)
    {
        // Transition rendern (zeichnet alte und neue Szene intern)
        if (_activeTransition != null && _oldScene != null && _newScene != null)
        {
            _activeTransition.Render(canvas, bounds,
                (c, b) => _oldScene.Render(c, b),
                (c, b) => _newScene.Render(c, b));
            return;
        }

        // Aktive Szene rendern
        CurrentScene?.Render(canvas, bounds);

        // Overlays rendern (transparent drüber)
        for (int i = 0; i < _overlays.Count; i++)
            _overlays[i].Render(canvas, bounds);
    }

    /// <summary>
    /// Input an Overlays (von oben) oder aktive Szene weiterleiten.
    /// Während Transition wird kein Input akzeptiert.
    /// </summary>
    public void HandleInput(InputAction action, SKPoint position)
    {
        if (_activeTransition != null) return;

        // Overlays bekommen Input zuerst (oberstes konsumiert, außer ConsumesInput=false)
        if (_overlays.Count > 0)
        {
            var topOverlay = _overlays[^1];
            topOverlay.HandleInput(action, position);
            if (topOverlay.ConsumesInput) return;
        }

        CurrentScene?.HandleInput(action, position);
    }

    /// <summary>
    /// Roher Pointer-Down an Overlays oder aktive Szene.
    /// </summary>
    public void HandlePointerDown(SKPoint position)
    {
        if (_activeTransition != null) return;

        if (_overlays.Count > 0)
        {
            var topOverlay = _overlays[^1];
            topOverlay.HandlePointerDown(position);
            if (topOverlay.ConsumesInput) return;
        }

        CurrentScene?.HandlePointerDown(position);
    }

    /// <summary>
    /// Roher Pointer-Move an Overlays oder aktive Szene.
    /// </summary>
    public void HandlePointerMove(SKPoint position)
    {
        if (_activeTransition != null) return;

        if (_overlays.Count > 0)
        {
            var topOverlay = _overlays[^1];
            topOverlay.HandlePointerMove(position);
            if (topOverlay.ConsumesInput) return;
        }

        CurrentScene?.HandlePointerMove(position);
    }

    /// <summary>
    /// Roher Pointer-Up an Overlays oder aktive Szene.
    /// </summary>
    public void HandlePointerUp(SKPoint position)
    {
        if (_activeTransition != null) return;

        if (_overlays.Count > 0)
        {
            var topOverlay = _overlays[^1];
            topOverlay.HandlePointerUp(position);
            if (topOverlay.ConsumesInput) return;
        }

        CurrentScene?.HandlePointerUp(position);
    }

    /// <summary>
    /// Erstellt eine Szene per DI (Constructor Injection wird unterstützt).
    /// </summary>
    private T CreateScene<T>() where T : Scene
    {
        return ActivatorUtilities.CreateInstance<T>(_serviceProvider);
    }

    /// <summary>
    /// Startet einen Szenen-Übergang.
    /// </summary>
    private void StartTransition(TransitionEffect transition, Scene oldScene, Scene newScene, Action onComplete)
    {
        _activeTransition = transition;
        _activeTransition.Reset();
        _oldScene = oldScene;
        _newScene = newScene;
        _onTransitionComplete = onComplete;
    }
}
