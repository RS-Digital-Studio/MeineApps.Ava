using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.ViewModels;
using SkiaSharp;

namespace HandwerkerImperium.Views;

public partial class MainView : UserControl
{
    private MainViewModel? _vm;
    // Enum statt String. Frueher allokierte ActivePage.ToString() einen
    // neuen String pro PropertyChanged-Event — 15 PropertyChanged/s × ~10 bytes/String = 150 b/s
    // GC-Druck plus String-Equality-Compare statt enum-Compare im PropertyChanged-Handler.
    private HandwerkerImperium.Models.Enums.ActivePage? _lastActiveTab;

    // SkiaSharp Tab-Bar + Screen-Transitions + Background (+4)
    private readonly GameTabBarRenderer _tabBarRenderer = new();
    private readonly ScreenTransitionRenderer _transitionRenderer = new();
    private readonly GameBackgroundRenderer _backgroundRenderer = new();

    // Full-Screen Reward-Zeremonie (Phase 7)
    private readonly RewardCeremonyRenderer _ceremonyRenderer = new();

    // Prestige-Cinematic-Renderer (4 Phasen, 14s)
    private readonly PrestigeCinematicRenderer _prestigeCinematicRenderer = new();

    // Animierter Loading-Screen (Phase 10)
    private readonly LoadingScreenRenderer _loadingRenderer = new();
    private bool _loadingTipsInitialized;

    // DispatcherTimer durch IFrameClock-Subscription ersetzt.
    private Services.Interfaces.IFrameClock? _frameClock;
    private bool _renderActive;
    private float _renderTime;
    private float _lastTabSwitchTime;
    private SKRect _lastTabBarBounds;
    private SKRect _lastBackgroundBounds;
    private GameScreenType _currentScreenType = GameScreenType.Dashboard;
    private string[] _tabLabels = ["Workshop", "Empire", "Missions", "Guild", "Shop"];

    // Performance: Hintergrund und Tab-Bar gedrosselt, während Scroll pausiert
    private int _bgTickCounter;

    // Gecachte Badge-Counts (vermeidet Array-Allokation pro Frame)
    private readonly int[] _tabBadgeCounts = new int[5];

    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        // Escape schliesst den obersten Dialog (Desktop-Komfort).
        // Tunnel-Phase damit das vor evtl. Dialog-internen Handlern triggert.
        AddHandler(KeyDownEvent, OnGlobalKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Auf Desktop schliesst Escape den obersten sichtbaren Dialog.
    /// Aequivalent zum Android-Back-Button (HandleBackPressed). Auf Android ist Escape
    /// kein nativer Key — der Handler ist daher reine Desktop-UX.
    /// </summary>
    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _vm == null) return;
        if (_vm.HandleBackPressed())
            e.Handled = true;
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        StopRenderTimer();

        if (_vm != null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.PageTransitionStarting -= OnPageTransitionStarting;
            _vm.CelebrationRequested -= OnCelebrationRequested;
            _vm.CeremonyRequested -= OnCeremonyRequested;
            _vm.PrestigeCinematicRequested -= OnPrestigeCinematicRequested;
            _vm.PauseStateChanged -= OnPauseStateChanged;
            _vm = null;
        }

        // try/catch um jeden Renderer-Dispose. Frueher hat eine
        // Exception bei einem Dispose-Call den Rest der Kaskade abgebrochen, wodurch andere
        // Renderer ihr Native-Memory nicht freigaben.
        try { _tabBarRenderer.Dispose(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainView] TabBar.Dispose: {ex.Message}"); }
        try { _transitionRenderer.Dispose(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainView] Transition.Dispose: {ex.Message}"); }
        try { _backgroundRenderer.Dispose(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainView] Background.Dispose: {ex.Message}"); }
        try { _ceremonyRenderer.Dispose(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainView] Ceremony.Dispose: {ex.Message}"); }
        try { _prestigeCinematicRenderer.Dispose(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainView] PrestigeCinematic.Dispose: {ex.Message}"); }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Altes VM abmelden
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.PageTransitionStarting -= OnPageTransitionStarting;
            _vm.CelebrationRequested -= OnCelebrationRequested;
            _vm.CeremonyRequested -= OnCeremonyRequested;
            _vm.PrestigeCinematicRequested -= OnPrestigeCinematicRequested;
            _vm.PauseStateChanged -= OnPauseStateChanged;
        }

        _vm = DataContext as MainViewModel;

        if (_vm != null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            _vm.PageTransitionStarting += OnPageTransitionStarting;
            _vm.CelebrationRequested += OnCelebrationRequested;
            _vm.CeremonyRequested += OnCeremonyRequested;
            _vm.PrestigeCinematicRequested += OnPrestigeCinematicRequested;
            _vm.PauseStateChanged += OnPauseStateChanged;

            // Tab-Labels initialisieren
            RefreshTabLabels();

            // Render-Timer starten (für Tab-Bar + Transition + Hans)
            StartRenderTimer();
        }
    }

    /// <summary>
    /// Stoppt oder startet den Render-Timer entsprechend dem App-Pause-Status.
    /// Gefeuert von MainViewModel.PauseGameLoop()/ResumeGameLoop() via Android-Lifecycle.
    /// Spart Battery und Hintergrund-CPU wenn App minimiert/Screen gesperrt ist.
    /// </summary>
    private void OnPauseStateChanged(bool isPaused)
    {
        // FrameClock kennt globale Pause/Resume — andere Views sind davon ebenfalls betroffen.
        // Wir steuern hier nur unsere eigene Subscription, damit andere Subscriber
        // (z.B. MiniGames) nicht ungewollt pausieren.
        if (isPaused)
        {
            StopRenderTimer();
        }
        else if (!_renderActive)
        {
            StartRenderTimer();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Render-Timer (15fps - Tab-Bar, Background, Transition, Hans)
    // ═══════════════════════════════════════════════════════════════════════

    private void StartRenderTimer()
    {
        if (_renderActive) return;
        _frameClock ??= App.Services?.GetService(typeof(Services.Interfaces.IFrameClock)) as Services.Interfaces.IFrameClock;
        // Render-Intervall aus aktiver Grafikqualitaet (Low=10fps, Medium/High=15fps)
        _frameClock?.Subscribe(OnRenderTimerTick, Graphics.FpsProfile.MainView());
        _renderActive = true;
    }

    private void StopRenderTimer()
    {
        if (!_renderActive) return;
        _frameClock?.Unsubscribe(OnRenderTimerTick);
        _renderActive = false;
    }

    private void OnRenderTimerTick(object? sender, Services.Interfaces.FrameTickEventArgs e)
    {
        _renderTime += 0.066f; // 66ms Intervall (15fps)

        // Money-Animation aktualisieren (hat internen Early-Return wenn nicht aktiv)
        _vm?.UpdateMoneyAnimation();

        // Scroll-Status vom DashboardView abfragen (pausiert Canvas-Rendering)
        bool isScrolling = DashboardViewInstance?.IsScrolling == true;

        // Hintergrund alle 15 Ticks (~1fps) - während Scroll komplett pausieren
        _bgTickCounter++;
        if (_bgTickCounter >= 15 && !isScrolling)
        {
            _bgTickCounter = 0;
            _backgroundRenderer.UpdateParticles(1.0f, _currentScreenType, _lastBackgroundBounds);
            BackgroundCanvas?.InvalidateSurface();
        }

        // Tab-Bar: 15fps kurz nach Tab-Wechsel (Animation), sonst ~5fps
        // Während Scroll pausieren (TabBar ändert sich nicht beim Scrollen)
        if (_vm?.IsTabBarVisible == true && !isScrolling)
        {
            bool tabAnimActive = (_renderTime - _lastTabSwitchTime) < 0.5f; // 500ms nach Wechsel
            if (tabAnimActive || _bgTickCounter % 3 == 0)
                TabBarCanvas?.InvalidateSurface();
        }

        // Screen-Transition aktualisieren (wenn aktiv)
        if (_transitionRenderer.IsActive)
        {
            _transitionRenderer.Update(0.04f);
            TransitionCanvas?.InvalidateSurface();
        }

        // Reward-Zeremonie aktualisieren
        if (_ceremonyRenderer.IsActive)
        {
            _ceremonyRenderer.Update(0.04f);
            CeremonyCanvas?.InvalidateSurface();

            if (!_ceremonyRenderer.IsActive && CeremonyCanvas != null)
            {
                CeremonyCanvas.IsVisible = false;
            }
        }

        // Prestige-Cinematic
        if (_prestigeCinematicRenderer.IsActive)
        {
            _prestigeCinematicRenderer.Update(0.066f);
            PrestigeCinematicCanvas?.InvalidateSurface();

            if (!_prestigeCinematicRenderer.IsActive && PrestigeCinematicCanvas != null)
            {
                PrestigeCinematicCanvas.IsVisible = false;
            }
        }

        // Loading-Screen aktualisieren (solange sichtbar)
        if (_vm?.IsLoading == true)
        {
            LoadingCanvas?.InvalidateSurface();
        }

        // Meister Hans aktualisieren (delegiert an StoryDialog UserControl)
        if (_vm?.DialogVM.IsStoryDialogVisible == true)
        {
            StoryDialogControl?.UpdateHansAnimation();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Animierter Hintergrund ()
    // ═══════════════════════════════════════════════════════════════════════

    private void OnBackgroundPaintSurface(object? sender, Avalonia.Labs.Controls.SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var bounds = canvas.LocalClipBounds;
        _lastBackgroundBounds = bounds;

        _backgroundRenderer.Render(canvas, bounds, _currentScreenType, _renderTime);
    }

    /// <summary>
    /// Ermittelt den GameScreenType basierend auf ActivePage.
    /// ActivePage-Switch statt IsXxxActive-Kette → neue Pages werden nicht vergessen.
    /// </summary>
    private GameScreenType GetCurrentScreenType()
    {
        if (_vm == null) return GameScreenType.Dashboard;
        return _vm.ActivePage switch
        {
            ActivePage.Dashboard => GameScreenType.Dashboard,
            ActivePage.Buildings => GameScreenType.Buildings,
            ActivePage.Missionen => GameScreenType.Dashboard, // Missionen nutzen Dashboard-Hintergrund
            ActivePage.Guild or ActivePage.GuildResearch or ActivePage.GuildMembers or
            ActivePage.GuildInvite or ActivePage.GuildWarSeason or ActivePage.GuildBoss or
            ActivePage.GuildHall or ActivePage.GuildAchievements or ActivePage.GuildChat or
            ActivePage.GuildWar => GameScreenType.Guild,
            ActivePage.Shop => GameScreenType.Shop,
            ActivePage.Settings => GameScreenType.Settings,
            ActivePage.WorkerMarket => GameScreenType.Workers,
            ActivePage.Research => GameScreenType.Research,
            // Workshop-Detail, MiniGames, Turnier etc. → Workshop-Hintergrund
            _ => GameScreenType.Workshop
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SkiaSharp Tab-Bar
    // ═══════════════════════════════════════════════════════════════════════

    private void OnTabBarPaintSurface(object? sender, Avalonia.Labs.Controls.SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var bounds = canvas.LocalClipBounds;
        _lastTabBarBounds = bounds;

        var lockedTabs = _vm?.GetLockedTabs() ?? [];

        // Badge-Counts in gecachtes Array schreiben (keine Allokation pro Frame)
        if (_vm != null)
        {
            _tabBadgeCounts[0] = (_vm.HasPendingDelivery ? 1 : 0) + (_vm.CanActivateRush ? 1 : 0);
            _tabBadgeCounts[1] = _vm.HeaderVM.HasWorkerWarning ? 1 : 0;
            _tabBadgeCounts[2] = _vm.MissionsVM.ClaimableMissionsCount + (_vm.MissionsVM.HasFreeSpin ? 1 : 0);
            _tabBadgeCounts[3] = 0;
            _tabBadgeCounts[4] = 0;
        }

        var state = new TabBarState
        {
            ActiveTab = GetActiveTabIndex(),
            BadgeCounts = _tabBadgeCounts,
            Labels = _tabLabels,
            Time = _renderTime,
            TabSwitchTime = _lastTabSwitchTime,
            LockedTabs = lockedTabs,
            UnlockLevels = MainViewModel.TabUnlockLevels
        };

        _tabBarRenderer.Render(canvas, bounds, state);
    }

    private void OnTabBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm == null || sender is not Avalonia.Labs.Controls.SKCanvasView canvas) return;

        var pos = e.GetPosition(canvas);

        // Avalonia-Koordinaten → SkiaSharp-Koordinaten (DPI-Skalierung)
        float scaleX = _lastTabBarBounds.Width / (float)canvas.Bounds.Width;
        float scaleY = _lastTabBarBounds.Height / (float)canvas.Bounds.Height;
        float skiaX = (float)pos.X * scaleX;
        float skiaY = (float)pos.Y * scaleY;

        int tabIndex = _tabBarRenderer.HitTest(_lastTabBarBounds, skiaX, skiaY);
        if (tabIndex >= 0 && tabIndex != GetActiveTabIndex())
        {
            // Gesperrte Tabs: Hinweis anzeigen statt navigieren
            if (_vm.IsTabLocked(tabIndex))
            {
                var levelNeeded = MainViewModel.TabUnlockLevels[tabIndex];
                _vm.ShowLockedTabHint(levelNeeded);
                e.Handled = true;
                return;
            }

            // Screen-Transition starten
            _transitionRenderer.StartTransition(TransitionType.Dissolve);
            TransitionCanvas?.InvalidateSurface();

            // Tab-Wechsel ausführen
            ExecuteTabCommand(tabIndex);
            _lastTabSwitchTime = _renderTime;
        }

        e.Handled = true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Screen-Transition Overlay
    // ═══════════════════════════════════════════════════════════════════════

    private void OnTransitionPaintSurface(object? sender, Avalonia.Labs.Controls.SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (!_transitionRenderer.IsActive && _transitionRenderer.Progress < 0.01f) return;

        var bounds = canvas.LocalClipBounds;
        _transitionRenderer.Render(canvas, bounds);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Tab-Verwaltung
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gibt den Index des aktiven Haupt-Tabs zurück (0-4), oder -1 bei Detail-Views.
    /// </summary>
    private int GetActiveTabIndex()
    {
        if (_vm == null) return 0;
        if (_vm.IsDashboardActive) return 0;     // Werkstatt
        if (_vm.IsBuildingsActive) return 1;     // Imperium
        if (_vm.IsMissionenActive) return 2;     // Missionen
        if (_vm.IsGuildActive) return 3;         // Gilde
        if (_vm.IsShopActive) return 4;          // Shop
        return -1;
    }

    /// <summary>
    /// Führt den passenden SelectXxxTabCommand für den Tab-Index aus.
    /// </summary>
    private void ExecuteTabCommand(int tabIndex)
    {
        if (_vm == null) return;
        switch (tabIndex)
        {
            case 0: _vm.SelectDashboardTabCommand.Execute(null); break;
            case 1: _vm.SelectBuildingsTabCommand.Execute(null); break;  // Imperium
            case 2: _vm.SelectMissionenTabCommand.Execute(null); break;  // Missionen
            case 3: _vm.SelectGuildTabCommand.Execute(null); break;
            case 4: _vm.SelectShopTabCommand.Execute(null); break;
        }
    }

    /// <summary>
    /// Lokalisierte Tab-Labels vom ViewModel holen.
    /// </summary>
    private void RefreshTabLabels()
    {
        _tabLabels = _vm?.GetTabLabels() ?? ["Workshop", "Empire", "Missions", "Guild", "Shop"];
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Property-Changed + Content-Animation
    // ═══════════════════════════════════════════════════════════════════════

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // ActivePage feuert bei JEDEM Seitenwechsel (statt Whitelist einzelner IsXxxActive-Properties)
        if (e.PropertyName == nameof(MainViewModel.ActivePage))
        {
            // Hintergrund-Typ aktualisieren
            _currentScreenType = GetCurrentScreenType();

            // Fade-Animation auslösen — M-M04: Enum-Compare statt String-Allokation pro Event.
            var newTab = _vm?.ActivePage;
            if (newTab != null && newTab != _lastActiveTab)
            {
                _lastActiveTab = newTab;
                FadeInContentPanel();
            }
        }
    }

    // Gecachte FadeIn-Animation (vermeidet Allokation bei jedem Tab-Wechsel)
    private static readonly Animation s_fadeInAnimation = new()
    {
        Duration = TimeSpan.FromMilliseconds(150),
        Easing = new CubicEaseOut(),
        FillMode = FillMode.Forward,
        Children =
        {
            new KeyFrame
            {
                Cue = new Cue(0),
                Setters = { new Setter(OpacityProperty, 0.0) }
            },
            new KeyFrame
            {
                Cue = new Cue(1.0),
                Setters = { new Setter(OpacityProperty, 1.0) }
            }
        }
    };

    /// <summary>
    /// Setzt Opacity=0 VOR dem Binding-Update (verhindert Flimmern).
    /// Wird von PageTransitionStarting aufgerufen — feuert bevor ActivePage-Wert sich ändert.
    /// </summary>
    private void OnPageTransitionStarting()
    {
        var panel = this.FindControl<Panel>("ContentPanel");
        if (panel != null) panel.Opacity = 0;
    }

    /// <summary>
    /// Fade-In Animation für das ContentPanel bei Seitenwechsel.
    /// Animiert Opacity 0→1 (150ms, CubicEaseOut). Opacity=0 wurde bereits durch
    /// OnPageTransitionStarting gesetzt bevor die Bindings updaten.
    /// Safety-Net: Setzt Opacity=1 auch wenn Animation fehlschlägt (Android-Stabilität).
    /// </summary>
    private async void FadeInContentPanel()
    {
        var panel = this.FindControl<Panel>("ContentPanel");
        if (panel == null) return;

        try
        {
            await s_fadeInAnimation.RunAsync(panel);
        }
        catch (Exception)
        {
            // Animation-Fehler nicht kritisch (View evtl. bereits entladen)
        }
        finally
        {
            // Safety-Net: Sicherstellen dass Content IMMER sichtbar wird,
            // auch wenn Animation gecancelt/fehlgeschlagen (Android-Robustheit)
            if (panel.Opacity < 1.0)
                panel.Opacity = 1.0;
        }
    }

    private void OnCelebrationRequested()
    {
        CelebrationCanvas?.ShowConfetti();
    }

    private void OnCeremonyRequested(CeremonyType type, string title, string subtitle)
    {
        _ceremonyRenderer.Start(type, title, subtitle);
        if (CeremonyCanvas != null)
        {
            CeremonyCanvas.IsVisible = true;
            CeremonyCanvas.InvalidateSurface();
        }
    }

    private void OnCeremonyPaintSurface(object? sender, Avalonia.Labs.Controls.SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (!_ceremonyRenderer.IsActive) return;
        var bounds = canvas.LocalClipBounds;
        _ceremonyRenderer.Render(canvas, bounds);
    }

    private void OnCeremonyTapped(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        _ceremonyRenderer.Dismiss();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Prestige-Cinematic (Render + Tap-Handling)
    // ═══════════════════════════════════════════════════════════════════════

    private void OnPrestigeCinematicRequested(HandwerkerImperium.Models.PrestigeCinematicData data)
    {
        _prestigeCinematicRenderer.Start(data);
        if (PrestigeCinematicCanvas != null)
        {
            PrestigeCinematicCanvas.IsVisible = true;
            PrestigeCinematicCanvas.InvalidateSurface();
        }
    }

    private void OnPrestigeCinematicPaintSurface(object? sender, Avalonia.Labs.Controls.SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        if (!_prestigeCinematicRenderer.IsActive) return;
        var bounds = canvas.LocalClipBounds;
        _prestigeCinematicRenderer.Render(canvas, bounds);
    }

    private void OnPrestigeCinematicTapped(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // 3-Stufen-Logik:
        //  (a) Tap im Skip-Button-Bereich (-3) = Skip
        //  (b) Tap irgendwo waehrend -3 = Skip (groesserer Touch-Bereich)
        //  (c) Tap in Reward-Phase = Dismiss (Tap-to-Continue)
        if (_prestigeCinematicRenderer.IsReadyForDismiss)
        {
            _prestigeCinematicRenderer.Dismiss();
            _vm?.OnPrestigeCinematicDismissed();
            return;
        }

        if (_prestigeCinematicRenderer.IsSkipEnabled)
        {
            // Skip — sowohl bei Tap im Button als auch im Restbereich (P0.3 fordert "Skip ab 5 s sichtbar")
            _prestigeCinematicRenderer.Skip();
            _vm?.OnPrestigeCinematicSkipped();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Loading-Screen (Phase 10)
    // ═══════════════════════════════════════════════════════════════════════

    private void OnLoadingPaintSurface(object? sender, Avalonia.Labs.Controls.SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        // Lokalisierte Tipps einmalig vom ViewModel holen
        if (!_loadingTipsInitialized)
        {
            _loadingRenderer.SetTips(_vm?.GetLoadingTips() ?? [
                "Tip: Hold the upgrade button for rapid leveling!",
                "Tip: Higher worker tiers earn significantly more!",
                "Tip: Visit daily for login rewards!",
                "Tip: Prestige unlocks new bonuses and workshops!",
                "Tip: Reputation above 70 brings extra orders!",
                "Tip: Master tools give permanent income bonuses!"
            ]);
            _loadingTipsInitialized = true;
        }

        var bounds = canvas.LocalClipBounds;
        _loadingRenderer.Render(canvas, bounds, _renderTime);
    }

}
