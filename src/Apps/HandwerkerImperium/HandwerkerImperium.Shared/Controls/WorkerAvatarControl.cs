using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Models.Enums;
using SkiaSharp;

namespace HandwerkerImperium.Controls;

/// <summary>
/// Wiederverwendbares Control das einen Pixel-Art Worker-Avatar per SkiaSharp rendert.
/// Nutzt den WorkerAvatarRenderer mit Cache.
/// </summary>
public class WorkerAvatarControl : Control
{
    // ═══════════════════════════════════════════════════════════════════════
    // STYLED PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Worker-ID als Seed fuer deterministische Avatar-Generierung.
    /// </summary>
    public static readonly StyledProperty<string> IdSeedProperty =
        AvaloniaProperty.Register<WorkerAvatarControl, string>(nameof(IdSeed), string.Empty);

    /// <summary>
    /// Worker-Tier bestimmt die Helm-Farbe.
    /// </summary>
    public static readonly StyledProperty<WorkerTier> TierProperty =
        AvaloniaProperty.Register<WorkerAvatarControl, WorkerTier>(nameof(Tier), WorkerTier.E);

    /// <summary>
    /// Stimmung (0-100), bestimmt Gesichtsausdruck.
    /// </summary>
    public static readonly StyledProperty<decimal> MoodProperty =
        AvaloniaProperty.Register<WorkerAvatarControl, decimal>(nameof(Mood), 70m);

    /// <summary>
    /// Groesse des Avatars in dp.
    /// </summary>
    public static readonly StyledProperty<int> AvatarSizeProperty =
        AvaloniaProperty.Register<WorkerAvatarControl, int>(nameof(AvatarSize), 48);

    /// <summary>
    /// Geschlecht: true = weiblich, false = maennlich.
    /// </summary>
    public static readonly StyledProperty<bool> IsFemaleProperty =
        AvaloniaProperty.Register<WorkerAvatarControl, bool>(nameof(IsFemale));

    public string IdSeed
    {
        get => GetValue(IdSeedProperty);
        set => SetValue(IdSeedProperty, value);
    }

    public WorkerTier Tier
    {
        get => GetValue(TierProperty);
        set => SetValue(TierProperty, value);
    }

    public decimal Mood
    {
        get => GetValue(MoodProperty);
        set => SetValue(MoodProperty, value);
    }

    public int AvatarSize
    {
        get => GetValue(AvatarSizeProperty);
        set => SetValue(AvatarSizeProperty, value);
    }

    public bool IsFemale
    {
        get => GetValue(IsFemaleProperty);
        set => SetValue(IsFemaleProperty, value);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FELDER
    // ═══════════════════════════════════════════════════════════════════════

    // Gemeinsamer Zeitgeber für animierte Rahmen + Idle-Animationen (alle Instanzen)
    private static readonly Stopwatch _sharedStopwatch = Stopwatch.StartNew();

    // 6 Hauttöne (identisch zu WorkerAvatarRenderer - für Blinzel-Overlay)
    private static readonly SKColor[] SkinTones =
    [
        new SKColor(0xFF, 0xDB, 0xAC),
        new SKColor(0xF1, 0xC2, 0x7D),
        new SKColor(0xE0, 0xAC, 0x69),
        new SKColor(0xC6, 0x8C, 0x53),
        new SKColor(0x8D, 0x5E, 0x3C),
        new SKColor(0x6E, 0x40, 0x20)
    ];

    // Gemeinsamer Timer für ALLE Instanzen (statt pro-Instanz 20fps Timer)
    private static DispatcherTimer? s_sharedTimer;
    private static readonly List<WeakReference<WorkerAvatarControl>> s_instances = new();
    private static readonly object s_lock = new();

    // Gecachte SKPaint-Instanzen (statt "using var paint = new SKPaint" pro Frame)
    private static readonly SKPaint s_bitmapPaint = new() { IsAntialias = false };
    private static readonly SKPaint s_blinkPaint = new() { IsAntialias = false };

    private readonly SKCanvasView _canvasView;
    private SKBitmap? _currentBitmap;
    private bool _needsAnimation;
    private bool _hasAIPortrait;
    private bool _aiCheckDone; // Guard: AI-Nachladeprüfung nur einmal durchführen
    private int _stableHash;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public WorkerAvatarControl()
    {
        _canvasView = new SKCanvasView();
        _canvasView.PaintSurface += OnPaintSurface;

        // SKCanvasView als visuelles Kind einhaengen
        ((ISetLogicalParent)_canvasView).SetParent(this);
        VisualChildren.Add(_canvasView);
        LogicalChildren.Add(_canvasView);

        // Bei Property-Aenderungen neu rendern
        IdSeedProperty.Changed.AddClassHandler<WorkerAvatarControl>((c, _) => c.InvalidateAvatar());
        TierProperty.Changed.AddClassHandler<WorkerAvatarControl>((c, _) => c.InvalidateAvatar());
        MoodProperty.Changed.AddClassHandler<WorkerAvatarControl>((c, _) => c.InvalidateAvatar());
        AvatarSizeProperty.Changed.AddClassHandler<WorkerAvatarControl>((c, _) => c.InvalidateAvatar());
        IsFemaleProperty.Changed.AddClassHandler<WorkerAvatarControl>((c, _) => c.InvalidateAvatar());
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RENDERING
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Worker-Tier auf Rarity-Stufe mappen für Rahmen-Rendering.
    /// F/E=Common, D/C=Uncommon, B/A=Rare, S/SS=Epic, SSS/Legendary=Legendary.
    /// </summary>
    private static Rarity TierToRarity(WorkerTier tier) => tier switch
    {
        WorkerTier.F or WorkerTier.E => Rarity.Common,
        WorkerTier.D or WorkerTier.C => Rarity.Uncommon,
        WorkerTier.B or WorkerTier.A => Rarity.Rare,
        WorkerTier.S or WorkerTier.SS => Rarity.Epic,
        _ => Rarity.Legendary // SSS, Legendary
    };

    /// <summary>
    /// Bitmap neu generieren und Canvas invalidieren.
    /// </summary>
    private void InvalidateAvatar()
    {
        // Bitmap wird vom Cache verwaltet, NICHT hier disposen
        _currentBitmap = null;
        _aiCheckDone = false; // Bei Property-Änderung erneut prüfen dürfen

        // Neues Bitmap generieren
        var idStr = IdSeed ?? string.Empty;
        _stableHash = GetStableHash(idStr);

        int renderSize = AvatarSize switch
        {
            <= 32 => 32,
            <= 64 => 64,
            _ => 128
        };

        _currentBitmap = WorkerAvatarRenderer.RenderAvatar(
            idStr, Tier, Mood, renderSize, IsFemale);
        _hasAIPortrait = WorkerAvatarRenderer.HasAIPortrait(Tier, IsFemale);

        // Animations-Timer: Rarity-Rahmen (Uncommon+) ODER Idle-Animation (>=56dp)
        var rarity = TierToRarity(Tier);
        if (rarity >= Rarity.Uncommon || AvatarSize >= 56)
            RegisterForAnimation();
        else
            UnregisterFromAnimation();

        _canvasView.InvalidateSurface();
    }

    /// <summary>
    /// Stabiler Hash aus String (identisch zu WorkerAvatarRenderer).
    /// </summary>
    private static int GetStableHash(string input)
    {
        if (string.IsNullOrEmpty(input)) return 0;
        unchecked
        {
            int hash = 17;
            foreach (char c in input)
                hash = hash * 31 + c;
            return hash;
        }
    }

    /// <summary>
    /// Registriert diese Instanz beim gemeinsamen Animations-Timer.
    /// Ein einziger Timer für alle WorkerAvatarControls statt pro-Instanz (160→20 Ticks/s).
    /// </summary>
    private void RegisterForAnimation()
    {
        _needsAnimation = true;
        lock (s_lock)
        {
            s_instances.Add(new WeakReference<WorkerAvatarControl>(this));
            EnsureSharedTimerRunning();
        }
    }

    private void UnregisterFromAnimation()
    {
        _needsAnimation = false;
        // Aufräumen passiert im Timer-Tick (WeakReference wird automatisch entfernt)
    }

    private static void EnsureSharedTimerRunning()
    {
        if (s_sharedTimer != null) return;
        s_sharedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) }; // 20fps
        s_sharedTimer.Tick += OnSharedTimerTick;
        s_sharedTimer.Start();
    }

    private static void OnSharedTimerTick(object? sender, EventArgs e)
    {
        lock (s_lock)
        {
            // Alle lebenden Instanzen invalidieren, tote entfernen
            for (int i = s_instances.Count - 1; i >= 0; i--)
            {
                if (s_instances[i].TryGetTarget(out var control) && control._needsAnimation)
                {
                    control._canvasView.InvalidateSurface();
                }
                else
                {
                    s_instances.RemoveAt(i);
                }
            }

            // Timer stoppen wenn keine Instanzen mehr
            if (s_instances.Count == 0 && s_sharedTimer != null)
            {
                s_sharedTimer.Stop();
                s_sharedTimer = null;
            }
        }
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (_currentBitmap == null)
        {
            // Erstes Rendern: Bitmap generieren
            InvalidateAvatar();
            if (_currentBitmap == null) return;
        }

        // AI-Portrait-Nachladeprüfung: Wenn beim ersten Render nur Pixel-Art
        // verfügbar war, einmalig prüfen ob das AI-Portrait inzwischen geladen ist
        if (!_hasAIPortrait && !_aiCheckDone && WorkerAvatarRenderer.HasAIPortrait(Tier, IsFemale))
        {
            _aiCheckDone = true;
            InvalidateAvatar();
        }

        var bounds = _canvasView.CanvasSize;
        var fullRect = new SKRect(0, 0, (float)bounds.Width, (float)bounds.Height);
        var rarity = TierToRarity(Tier);
        float time = (float)_sharedStopwatch.Elapsed.TotalSeconds;

        // Avatar leicht einrücken um Platz für Rahmen zu lassen
        float frameInset = rarity >= Rarity.Epic ? 3f : (rarity >= Rarity.Uncommon ? 2f : 1f);
        var avatarRect = new SKRect(frameInset, frameInset, fullRect.Right - frameInset, fullRect.Bottom - frameInset);

        // Idle-Animation: Subtiles Atmen (vertikale Sinus-Oszillation, ±1dp)
        if (AvatarSize >= 56)
        {
            float breathOffset = _stableHash * 0.7f; // Jeder Avatar atmet leicht versetzt
            float breathY = MathF.Sin(time * 1.8f + breathOffset) * 1.2f;
            avatarRect.Offset(0, breathY);
        }

        var srcRect = new SKRect(0, 0, _currentBitmap.Width, _currentBitmap.Height);

        // Bitmap in die eingerückte Fläche zeichnen (statischer Paint, keine Allokation pro Frame)
        canvas.DrawBitmap(_currentBitmap, srcRect, avatarRect, s_bitmapPaint);

        // Idle-Animation: Blinzeln nur bei Pixel-Art (bei AI-Portraits nicht sinnvoll)
        if (AvatarSize >= 56 && !_hasAIPortrait)
        {
            DrawBlinkOverlay(canvas, avatarRect, time);
        }

        // Rarity-Rahmen über dem Avatar zeichnen
        RarityFrameRenderer.DrawRarityFrame(canvas, fullRect, rarity, time, 4f);
    }

    /// <summary>
    /// Zeichnet ein Blinzel-Overlay über die Augen (Hautfarben-Rect).
    /// Blinkel-Intervall: 3-5s (hash-basiert), Dauer: ~150ms.
    /// </summary>
    private void DrawBlinkOverlay(SKCanvas canvas, SKRect avatarRect, float time)
    {
        // Blinzel-Timing: Hash-basiertes Intervall (3-5s)
        float blinkInterval = 3f + Math.Abs(_stableHash % 200) / 100f;
        float blinkPhase = time % blinkInterval;
        float blinkDuration = 0.15f;

        // Nur während der Blinzel-Phase zeichnen
        if (blinkPhase > blinkDuration) return;

        // Hautton aus Hash (identisch zu WorkerAvatarRenderer)
        int skinIndex = Math.Abs(_stableHash) % SkinTones.Length;
        var skinColor = SkinTones[skinIndex];

        float w = avatarRect.Width;
        float h = avatarRect.Height;

        // Augenposition relativ zum Avatar (aus WorkerAvatarRenderer: eyeY=17/32, leftX=13/32, rightX=19/32)
        float eyeY = avatarRect.Top + h * 0.53f;
        float leftEyeX = avatarRect.Left + w * 0.40f;
        float rightEyeX = avatarRect.Left + w * 0.59f;
        float eyeWidth = w * 0.14f;
        float eyeHeight = h * 0.06f;

        // Statischer Paint mit dynamischer Farbe (keine Allokation pro Frame)
        s_blinkPaint.Color = skinColor;
        canvas.DrawRect(leftEyeX - eyeWidth / 2, eyeY - eyeHeight / 2, eyeWidth, eyeHeight, s_blinkPaint);
        canvas.DrawRect(rightEyeX - eyeWidth / 2, eyeY - eyeHeight / 2, eyeWidth, eyeHeight, s_blinkPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LAYOUT
    // ═══════════════════════════════════════════════════════════════════════

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = new Size(AvatarSize, AvatarSize);
        _canvasView.Measure(size);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rect = new Rect(0, 0, AvatarSize, AvatarSize);
        _canvasView.Arrange(rect);
        return new Size(AvatarSize, AvatarSize);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CLEANUP
    // ═══════════════════════════════════════════════════════════════════════

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UnregisterFromAnimation();
        // Bitmap wird vom Cache verwaltet, NICHT hier disposen
        _currentBitmap = null;
    }
}
