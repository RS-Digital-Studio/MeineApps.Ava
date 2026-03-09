namespace RebornSaga.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using MeineApps.Core.Ava.Services;
using RebornSaga.Engine;
using RebornSaga.Scenes;
using SkiaSharp;
using System;
using System.Threading.Tasks;

/// <summary>
/// Haupt-ViewModel - delegiert alles an SceneManager und InputManager.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly SceneManager _sceneManager;
    private readonly InputManager _inputManager;
    private readonly BackPressHelper _backPressHelper = new();

    /// <summary>
    /// Event für Android-Toast bei Double-Back-to-Exit.
    /// </summary>
    public event Action<string>? ExitHintRequested;

    public MainViewModel(SceneManager sceneManager)
    {
        _sceneManager = sceneManager;
        _inputManager = new InputManager(sceneManager);

        _backPressHelper.ExitHintRequested += msg => ExitHintRequested?.Invoke(msg);

        // Titelbildschirm als erste Szene laden
        _sceneManager.ChangeScene<TitleScene>();
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
        return _backPressHelper.HandleDoubleBack("Nochmal drücken zum Beenden");
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
}
