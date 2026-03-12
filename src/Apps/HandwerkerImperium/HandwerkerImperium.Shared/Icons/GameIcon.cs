using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using HandwerkerImperium.Services;
using SkiaSharp;

namespace HandwerkerImperium.Icons;

/// <summary>
/// Custom Icon-Control das Bitmap-Icons rendert (AI-generiert + programmatisch).
/// AI-Icons: Vollfarbig gerendert (warme Cartoon-Farben, Foreground wird ignoriert).
/// UI-Icons (Pfeile, Chevrons etc.): Getintet via Foreground (weisse Formen als Maske).
/// Erbt von TemplatedControl (hat Foreground) - AXAML bleibt unveraendert.
/// </summary>
public class GameIcon : TemplatedControl
{
    public static readonly StyledProperty<GameIconKind> KindProperty =
        AvaloniaProperty.Register<GameIcon, GameIconKind>(nameof(Kind));

    public GameIconKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    // Cache: GameIconKind -> Avalonia Bitmap (pro Icon nur 1x konvertiert)
    private static readonly ConcurrentDictionary<GameIconKind, Bitmap?> _bitmapCache = new();
    // Cache: GameIconKind -> ImageBrush fuer tintable Icons (vermeidet GC bei Render)
    private static readonly ConcurrentDictionary<GameIconKind, ImageBrush> _brushCache = new();
    private static readonly ConcurrentDictionary<GameIconKind, string> _pathMap = new();
    private static IGameAssetService? _assetService;
    private static bool _allPreloaded;

    /// <summary>
    /// Programmatische UI-Icons die per Foreground getintet werden.
    /// Alle anderen Icons werden vollfarbig gerendert.
    /// Zentrale Liste - GameIconRenderer liest von hier (keine Duplikation).
    /// </summary>
    internal static readonly HashSet<GameIconKind> TintableIcons = new()
    {
        GameIconKind.ArrowDown, GameIconKind.ArrowDownBold,
        GameIconKind.ArrowLeft, GameIconKind.ArrowRight, GameIconKind.ArrowUpBold,
        GameIconKind.ChevronRight, GameIconKind.ChevronDown, GameIconKind.ChevronUp,
        GameIconKind.Close, GameIconKind.Plus, GameIconKind.Check,
        GameIconKind.SwapHorizontal, GameIconKind.ExitToApp,
        GameIconKind.Loading, GameIconKind.Refresh, GameIconKind.Stop,
        GameIconKind.PlayCircle, GameIconKind.TrendingUp, GameIconKind.TrendingDown,
        GameIconKind.ViewGrid, GameIconKind.KeyboardReturn, GameIconKind.ShareVariant,
    };

    /// <summary>
    /// Initialisiert das Icon-System mit dem Asset-Service.
    /// Muss in App.axaml.cs nach DI-Setup aufgerufen werden.
    /// </summary>
    public static void Initialize(IGameAssetService? assetService)
    {
        _assetService = assetService;
        // Auch GameIconRenderer fuer SkiaSharp-Rendering initialisieren
        GameIconRenderer.Initialize(assetService);
    }

    /// <summary>
    /// Laedt alle Icon-Bitmaps beim App-Start.
    /// Sollte waehrend der Loading-Pipeline aufgerufen werden.
    /// Bei 128x128 WebP-Icons: ~224 Dateien x ~5KB = ~1.1 MB I/O, ~200ms.
    /// </summary>
    public static async System.Threading.Tasks.Task PreloadAllAsync()
    {
        if (_assetService == null || _allPreloaded) return;

        var paths = new System.Collections.Generic.List<string>();
        foreach (GameIconKind kind in Enum.GetValues<GameIconKind>())
        {
            if (kind == GameIconKind.None) continue;
            paths.Add(GetIconPath(kind));
        }

        // Alle Icons parallel laden (SKBitmap via GameAssetService)
        await _assetService.PreloadAsync(paths);

        // SKBitmap -> Avalonia Bitmap konvertieren
        foreach (GameIconKind kind in Enum.GetValues<GameIconKind>())
        {
            if (kind == GameIconKind.None) continue;
            GetOrCreateBitmap(kind);
        }

        _allPreloaded = true;
    }

    static GameIcon()
    {
        // Render bei Kind- oder Foreground-Aenderung neu triggern
        AffectsRender<GameIcon>(KindProperty, ForegroundProperty);
    }

    public GameIcon()
    {
        Width = 28;
        Height = 28;
    }

    // Retry-Zaehler pro Instanz: max 3 Versuche nach fehlgeschlagenem Render
    private int _loadRetries;

    public override void Render(DrawingContext context)
    {
        var bitmap = GetOrCreateBitmap(Kind);
        if (bitmap == null)
        {
            // Bitmap noch nicht geladen - Neuzeichnung nach 150ms (max 3 Retries)
            // PreloadAllAsync deckt 99% ab, dies ist ein Safety-Net
            if (_loadRetries++ < 3)
            {
                var timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                timer.Tick += (_, _) => { timer.Stop(); InvalidateVisual(); };
                timer.Start();
            }
            return;
        }
        _loadRetries = 0;

        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);

        // UI-Icons (Pfeile, Chevrons etc.) werden per Foreground getintet
        // AI-Icons (Objekte) werden in ihren originalen Farben gerendert
        var fg = Foreground;
        if (fg != null && TintableIcons.Contains(Kind))
        {
            var brush = _brushCache.GetOrAdd(Kind, _ => new ImageBrush(bitmap)
            {
                Stretch = Stretch.Uniform,
            });
            using (context.PushOpacityMask(brush, bounds))
            {
                context.FillRectangle(fg, bounds);
            }
        }
        else
        {
            context.DrawImage(bitmap, bounds);
        }
    }

    private static Bitmap? GetOrCreateBitmap(GameIconKind kind)
    {
        if (kind == GameIconKind.None || _assetService == null) return null;

        if (_bitmapCache.TryGetValue(kind, out var cached))
            return cached;

        var path = GetIconPath(kind);
        var skBitmap = _assetService.GetBitmap(path);

        if (skBitmap == null)
        {
            // Async-Load starten falls noch nicht geladen
            _ = _assetService.LoadBitmapAsync(path);
            return null;
        }

        // SKBitmap -> Avalonia Bitmap konvertieren (einmalig, gecacht)
        var avBitmap = ConvertToAvaloniaBitmap(skBitmap);
        _bitmapCache.TryAdd(kind, avBitmap);
        return avBitmap;
    }

    /// <summary>
    /// Konvertiert SKBitmap zu Avalonia Bitmap.
    /// Fuer 128x128 Icons ist PNG-Encoding schnell (~0.5ms pro Icon).
    /// </summary>
    private static Bitmap? ConvertToAvaloniaBitmap(SKBitmap skBitmap)
    {
        try
        {
            using var data = skBitmap.Encode(SKEncodedImageFormat.Png, 100);
            if (data == null) return null;

            using var stream = new MemoryStream();
            data.SaveTo(stream);
            stream.Position = 0;
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Konvertiert GameIconKind zu Asset-Pfad (PascalCase -> snake_case).
    /// Identisch mit GameIconRenderer.GetIconAssetPath().
    /// </summary>
    private static string GetIconPath(GameIconKind kind)
    {
        return _pathMap.GetOrAdd(kind, k =>
        {
            var name = Regex.Replace(k.ToString(), "([a-z])([A-Z])", "$1_$2").ToLowerInvariant();
            return $"icons/{name}.webp";
        });
    }

    /// <summary>
    /// Leert den Avalonia-Bitmap-Cache (z.B. bei App-Shutdown).
    /// </summary>
    public static void ClearCache()
    {
        foreach (var entry in _bitmapCache.Values)
            entry?.Dispose();
        _bitmapCache.Clear();
        _brushCache.Clear();
        _allPreloaded = false;
    }
}
