namespace RebornSaga.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Ava.Services;
using RebornSaga.Engine;
using RebornSaga.Scenes;
using RebornSaga.Services;
using SkiaSharp;
using System;
using System.Threading.Tasks;

/// <summary>
/// Haupt-ViewModel - delegiert alles an SceneManager und InputManager.
/// </summary>
public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly SceneManager _sceneManager;
    private readonly InputManager _inputManager;
    private readonly ILocalizationService _localization;
    private readonly IAudioService _audioService;
    private readonly IAppLifecycleService _lifecycle;
    private readonly BackPressHelper _backPressHelper = new();
    private bool _disposed;

    /// <summary>
    /// Event für Android-Toast bei Double-Back-to-Exit.
    /// </summary>
    public event Action<string>? ExitHintRequested;

    /// <summary>
    /// Wird gefeuert, wenn die App in den Hintergrund geht — die View stoppt daraufhin den
    /// 60fps-Game-Loop-Timer (Akku). Verlustfrei: das Spiel ist deltaTime-basiert.
    /// </summary>
    public event Action? GameLoopPauseRequested;

    /// <summary>
    /// Wird gefeuert, wenn die App in den Vordergrund zurückkehrt — die View startet den
    /// Game-Loop-Timer neu (mit zurückgesetztem Delta, kein Sprung).
    /// </summary>
    public event Action? GameLoopResumeRequested;

    public MainViewModel(
        SceneManager sceneManager,
        ILocalizationService localization,
        IAudioService audioService,
        IAppLifecycleService lifecycle)
    {
        _sceneManager = sceneManager;
        _localization = localization;
        _audioService = audioService;
        _lifecycle = lifecycle;
        _inputManager = new InputManager(sceneManager);

        _backPressHelper.ExitHintRequested += msg => ExitHintRequested?.Invoke(msg);

        // App-Lifecycle abonnieren (Android speist NotifyPaused/Resumed in MainActivity).
        _lifecycle.Paused += OnAppPaused;
        _lifecycle.Resumed += OnAppResumed;

        // Asset-Download-Prüfung als erste Szene laden
        // (wechselt automatisch zu TitleScene wenn keine Downloads nötig)
        _sceneManager.ChangeScene<AssetDownloadScene>();
    }

    /// <summary>
    /// App ging in den Hintergrund: BGM pausieren und Game-Loop stoppen (Akku).
    /// Verlustfrei — Speicherstand und Story-Progress bleiben unberührt.
    /// </summary>
    private void OnAppPaused()
    {
        _audioService.PauseBgm();
        GameLoopPauseRequested?.Invoke();
    }

    /// <summary>
    /// App kam in den Vordergrund: Game-Loop neu starten und BGM fortsetzen.
    /// </summary>
    private void OnAppResumed()
    {
        GameLoopResumeRequested?.Invoke();
        _audioService.ResumeBgm();
    }

    /// <summary>
    /// Initialisiert alle Services (Skills, Items, Purchases).
    /// Wird von MainView bei OnAttachedToVisualTree aufgerufen.
    /// </summary>
    public async Task InitializeAsync()
    {
        await App.InitializeServicesAsync();
    }

    /// <summary>
    /// Android Back-Button: Overlays schließen → Szene poppen → Double-Back-to-Exit.
    /// </summary>
    public bool HandleBackPressed()
    {
        // 1. Overlays schließen (oberstes zuerst)
        if (_sceneManager.Overlays.Count > 0)
        {
            var topOverlay = _sceneManager.Overlays[^1];
            _sceneManager.HideOverlay(topOverlay);
            return true;
        }

        // 2. Szene poppen wenn mehr als TitleScene auf dem Stack
        if (_sceneManager.CurrentScene is not TitleScene)
        {
            _sceneManager.PopScene();
            return true;
        }

        // 3. Double-Back-to-Exit auf TitleScene
        var msg = _localization.GetString("PressBackAgainToExit") ?? "Press back again to exit";
        return _backPressHelper.HandleDoubleBack(msg);
    }

    /// <summary>
    /// Spiellogik pro Frame aktualisieren.
    /// </summary>
    public void Update(float deltaTime)
    {
        _sceneManager.Update(deltaTime);
    }

    /// <summary>
    /// Zeichnet den aktuellen Frame auf das SkiaSharp-Canvas.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds)
    {
        // Fallback wenn keine Szene aktiv
        if (_sceneManager.CurrentScene == null)
        {
            canvas.Clear(new SKColor(0x0D, 0x11, 0x17)); // Dunkler Hintergrund aus Palette
            return;
        }

        _sceneManager.Render(canvas, bounds);
    }

    /// <summary>
    /// Touch/Maus gedrückt.
    /// </summary>
    public void HandlePointerPressed(SKPoint position) => _inputManager.OnPointerDown(position);

    /// <summary>
    /// Touch/Maus bewegt.
    /// </summary>
    public void HandlePointerMoved(SKPoint position) => _inputManager.OnPointerMove(position);

    /// <summary>
    /// Touch/Maus losgelassen.
    /// </summary>
    public void HandlePointerReleased(SKPoint position) => _inputManager.OnPointerUp(position);

    /// <summary>
    /// Keyboard-Event (Desktop).
    /// </summary>
    public void HandleKeyDown(Avalonia.Input.Key key) => _inputManager.OnKeyDown(key);

    /// <summary>
    /// Meldet die Lifecycle-Event-Abos wieder ab (Singleton-Lifetime, aber sauberes Unsubscribe).
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifecycle.Paused -= OnAppPaused;
        _lifecycle.Resumed -= OnAppResumed;
    }
}
