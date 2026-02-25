using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.ViewModels;
using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerImperium.Views;

public partial class WorkshopView : UserControl
{
    private WorkshopViewModel? _workshopVm;
    private readonly WorkshopInteriorRenderer _interiorRenderer = new();
    private readonly WorkshopSceneRenderer _sceneRenderer = new();
    private readonly AnimationManager _animationManager = new();
    private DispatcherTimer? _renderTimer;
    private SKCanvasView? _workshopCanvas;
    private DateTime _lastRenderTime = DateTime.UtcNow;
    // Animations-Zustand fuer arbeitende Worker
    private float _workerAnimPhase;

    // Ambient-Partikel (Staub, Funken, Tropfen - schwebend im Hintergrund)
    private readonly SkiaParticleManager _ambientParticles = new(30);
    private readonly Random _ambientRng = new();
    private float _ambientEmitTimer;

    public WorkshopView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Altes VM abmelden
        if (_workshopVm != null)
        {
            _workshopVm.UpgradeEffectRequested -= OnUpgradeEffect;
            _workshopVm = null;
        }

        // Timer stoppen wenn kein neues VM kommt
        _renderTimer?.Stop();

        if (DataContext is WorkshopViewModel vm)
        {
            _workshopVm = vm;
            vm.UpgradeEffectRequested += OnUpgradeEffect;

            // Workshop-Canvas finden und Timer starten
            _workshopCanvas = this.FindControl<SKCanvasView>("WorkshopCanvas");
            if (_workshopCanvas != null)
            {
                _workshopCanvas.PaintSurface -= OnWorkshopPaintSurface;
                _workshopCanvas.PaintSurface += OnWorkshopPaintSurface;
                StartRenderLoop();
            }
        }
    }

    private void StartRenderLoop()
    {
        _renderTimer?.Stop();
        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) }; // 20fps
        _renderTimer.Tick += (_, _) =>
        {
            _workshopCanvas?.InvalidateSurface();
        };
        _renderTimer.Start();
    }

    private void OnWorkshopPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        canvas.Clear(SKColors.Transparent);

        // Workshop aus ViewModel holen
        var workshop = _workshopVm?.GetWorkshopForRendering();
        if (workshop != null)
        {
            // Dezenter Hintergrund (mit dynamischer Beleuchtung + Wand-Details)
            _interiorRenderer.Render(canvas, bounds, workshop, _workerAnimPhase);

            // Aktive Worker zählen (nicht ruhend, nicht trainierend)
            int activeWorkers = workshop.Workers.Count(w => !w.IsResting && !w.IsTraining);

            // Ambient-Partikel VOR der Szene zeichnen (liegen dahinter)
            _ambientParticles.Draw(canvas, withGlow: true);

            if (activeWorkers > 0)
            {
                // Geschwindigkeits-Multiplikator basierend auf Worker-Anzahl
                float speed = activeWorkers switch
                {
                    1 => 0.7f,
                    2 or 3 => 1.0f,
                    _ => 1.3f
                };

                int particleRate = activeWorkers switch
                {
                    1 => 1,
                    2 or 3 => 2,
                    _ => 3
                };

                int productCount = activeWorkers switch
                {
                    1 => 1,
                    2 or 3 => 2,
                    _ => 3
                };

                // Animierte Szene zeichnen
                _sceneRenderer.Render(canvas, bounds, workshop,
                    _workerAnimPhase, activeWorkers, speed, particleRate, productCount,
                    (x, y, color) => _animationManager.AddWorkParticle(x, y, color),
                    (x, y) => _animationManager.AddCoinParticle(x, y));

                // Ambient-Partikel emittieren (nur wenn Worker aktiv)
                EmitAmbientParticles(workshop.Type, activeWorkers, bounds.Width, bounds.Height);
            }
            else
            {
                // Leerlauf-Zustand: Gedimmte Szene mit Warnsymbol
                _sceneRenderer.RenderIdle(canvas, bounds, workshop);
            }
        }

        // AnimationManager (Partikel)
        var now = DateTime.UtcNow;
        var delta = (now - _lastRenderTime).TotalSeconds;
        _lastRenderTime = now;

        _workerAnimPhase += (float)delta;
        if (_workerAnimPhase > 1000f) _workerAnimPhase -= 1000f;

        // Ambient-Partikel updaten
        _ambientParticles.Update((float)delta);

        _animationManager.Update(delta);
        _animationManager.Render(canvas);
    }

    // =================================================================
    // Ambient-Partikel (Phase 7) - Workshop-spezifischer Schwebestaub
    // =================================================================

    private void EmitAmbientParticles(WorkshopType type, int activeWorkers, float canvasW, float canvasH)
    {
        // Emissions-Rate: ~0.5s Basis, schneller bei mehr Workern
        float emitInterval = activeWorkers switch
        {
            1 => 0.6f,
            2 or 3 => 0.4f,
            _ => 0.25f
        };

        _ambientEmitTimer += 0.05f; // ~50ms pro Frame
        if (_ambientEmitTimer < emitInterval) return;
        _ambientEmitTimer = 0;

        // Zufällige Position innerhalb der Canvas
        float x = (float)_ambientRng.NextDouble() * canvasW;
        float y = canvasH * 0.3f + (float)_ambientRng.NextDouble() * canvasH * 0.5f;

        SkiaParticle particle = type switch
        {
            // Sägestaub (warm-braun, schwebt leicht nach oben)
            WorkshopType.Carpenter => SkiaParticlePresets.CreateSparkle(
                _ambientRng, x, y, new SKColor(0xD2, 0xB4, 0x8C, 60)),

            // Wasserspritzer (blau, fallen nach unten)
            WorkshopType.Plumber => SkiaParticlePresets.CreateWaterDrop(
                _ambientRng, x, y, new SKColor(0x42, 0xA5, 0xF5, 50)),

            // Funken (gelb-orange, kurz leuchtend)
            WorkshopType.Electrician => SkiaParticlePresets.CreateSparkle(
                _ambientRng, x, y, new SKColor(0xFF, 0xC1, 0x07, 70)),

            // Farbdunst (bunt, langsam schwebend)
            WorkshopType.Painter => SkiaParticlePresets.CreateGlow(
                _ambientRng, x, y,
                new[] {
                    new SKColor(0xEC, 0x48, 0x99, 40),
                    new SKColor(0x42, 0xA5, 0xF5, 40),
                    new SKColor(0x66, 0xBB, 0x6A, 40),
                    new SKColor(0xFF, 0xCA, 0x28, 40)
                }[_ambientRng.Next(4)]),

            // Staub (grau, langsam)
            WorkshopType.Roofer => SkiaParticlePresets.CreateSparkle(
                _ambientRng, x, y, new SKColor(0x9E, 0x9E, 0x9E, 40)),

            // Mörtelstaub (hellgrau)
            WorkshopType.Contractor => SkiaParticlePresets.CreateSparkle(
                _ambientRng, x, y, new SKColor(0xBD, 0xBD, 0xBD, 50)),

            // Radiergummikrümel (weiß, sehr dezent)
            WorkshopType.Architect => SkiaParticlePresets.CreateSparkle(
                _ambientRng, x, y, new SKColor(0xF5, 0xF5, 0xF5, 30)),

            // Gold-Glitzer (gold, funkelnd)
            WorkshopType.GeneralContractor => SkiaParticlePresets.CreateSparkle(
                _ambientRng, x, y, new SKColor(0xFF, 0xD7, 0x00, 50)),

            _ => SkiaParticlePresets.CreateSparkle(
                _ambientRng, x, y, new SKColor(0xCC, 0xCC, 0xCC, 40))
        };

        _ambientParticles.Add(particle);
    }

    // ====================================================================
    // SkiaSharp LinearProgress Handler
    // ====================================================================

    private void OnPaintWorkshopMainLevelProgress(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        canvas.Clear(SKColors.Transparent);

        float progress = 0f;
        if (DataContext is WorkshopViewModel vm)
            progress = (float)vm.LevelProgress;

        LinearProgressVisualization.Render(canvas, bounds, progress,
            new SKColor(0xD9, 0x77, 0x06), new SKColor(0xF5, 0x9E, 0x0B),
            showText: false, glowEnabled: true);
    }

    private async void OnUpgradeEffect(object? sender, EventArgs e)
    {
        // Level-Badge Scale-Pop Animation
        var badge = this.FindControl<Border>("LevelBadge");
        if (badge != null)
        {
            await AnimationHelper.ScaleUpDownAsync(badge, 1.0, 1.25, TimeSpan.FromMilliseconds(250));
        }

        // Konfetti-Partikel bei Upgrade
        if (_workshopCanvas != null)
        {
            var bounds = _workshopCanvas.Bounds;
            _animationManager.AddLevelUpConfetti((float)bounds.Width / 2, (float)bounds.Height / 2);
        }
    }
}
