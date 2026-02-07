using SkiaSharp;

namespace StoreAssetGenerator;

class Program
{
    static string _baseDir = null!;
    static string _outputDir = null!;

    static void Main(string[] args)
    {
        _baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

        var apps = new AppDef[]
        {
            RechnerPlusApp.Create(),
            ZeitManagerApp.Create(),
            FinanzRechnerApp.Create(),
            HandwerkerRechnerApp.Create(),
            FitnessRechnerApp.Create(),
            WorkTimeProApp.Create(),
            HandwerkerImperiumApp.Create(),
            BomberBlastApp.Create(),
        };

        // Filter per CLI args
        var filter = args.Length > 0 ? args : null;

        foreach (var app in apps)
        {
            if (filter != null && !filter.Any(f => app.Name.Contains(f, StringComparison.OrdinalIgnoreCase)))
                continue;

            _outputDir = Path.Combine(_baseDir, "Releases", app.Name);
            Directory.CreateDirectory(_outputDir);
            Console.WriteLine($"\n{'='} {app.Name} {'='}");
            Console.WriteLine($"Ausgabe: {_outputDir}");

            Gfx.OutputDir = _outputDir;

            app.DrawIcon(512);
            app.DrawFeatureGraphic();

            for (int i = 0; i < app.PhoneScreenshots.Length; i++)
            {
                var (banner, mockup) = app.PhoneScreenshots[i];
                Gfx.GenerateScreenshot(1080, 2340, $"phone_{i + 1}.png", banner, mockup, app.AccentColor);
            }

            for (int i = 0; i < app.TabletScreenshots.Length; i++)
            {
                var (banner, mockup) = app.TabletScreenshots[i];
                Gfx.GenerateScreenshot(1200, 1920, $"tablet_{i + 1}.png", banner, mockup, app.AccentColor);
            }

            var files = Directory.GetFiles(_outputDir, "*.png");
            Console.WriteLine($"  {files.Length} Dateien generiert");
        }

        Console.WriteLine("\nAlle Apps erfolgreich generiert!");
    }
}

// App-Definition
record AppDef(
    string Name,
    SKColor AccentColor,
    Action<int> DrawIcon,
    Action DrawFeatureGraphic,
    (string Banner, Action<SKCanvas, SKRect> Draw)[] PhoneScreenshots,
    (string Banner, Action<SKCanvas, SKRect> Draw)[] TabletScreenshots
);

// Shared Drawing Helpers
static class Gfx
{
    // Midnight Theme
    public static readonly SKColor Bg = SKColor.Parse("#0F172A");
    public static readonly SKColor Surface = SKColor.Parse("#1E293B");
    public static readonly SKColor Card = SKColor.Parse("#334155");
    public static readonly SKColor Primary = SKColor.Parse("#6366F1");
    public static readonly SKColor Secondary = SKColor.Parse("#8B5CF6");
    public static readonly SKColor Success = SKColor.Parse("#22C55E");
    public static readonly SKColor Warning = SKColor.Parse("#F59E0B");
    public static readonly SKColor Error = SKColor.Parse("#EF4444");
    public static readonly SKColor Gold = SKColor.Parse("#FFD700");
    public static readonly SKColor Cyan = SKColor.Parse("#22D3EE");
    public static readonly SKColor TextPrimary = SKColor.Parse("#F8FAFC");
    public static readonly SKColor TextSecondary = SKColor.Parse("#CBD5E1");
    public static readonly SKColor TextMuted = SKColor.Parse("#94A3B8");
    public static readonly SKColor Border = SKColor.Parse("#475569");

    public static string OutputDir { get; set; } = "";

    public static void RoundRect(SKCanvas c, float x, float y, float w, float h, float r, SKColor color)
    {
        using var p = new SKPaint { Color = color, IsAntialias = true };
        if (r > 0) c.DrawRoundRect(new SKRect(x, y, x + w, y + h), r, r, p);
        else c.DrawRect(x, y, w, h, p);
    }

    public static void Circle(SKCanvas c, float cx, float cy, float r, SKColor color)
    {
        using var p = new SKPaint { Color = color, IsAntialias = true };
        c.DrawCircle(cx, cy, r, p);
    }

    public static void Text(SKCanvas c, string text, float x, float y, float size, SKColor color, bool bold = false)
    {
        using var p = new SKPaint
        {
            Color = color, IsAntialias = true, TextSize = size,
            Typeface = SKTypeface.FromFamilyName("Segoe UI",
                bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };
        c.DrawText(text, x, y, p);
    }

    public static void TextC(SKCanvas c, string text, float cx, float y, float size, SKColor? color = null)
    {
        using var p = new SKPaint
        {
            Color = color ?? TextPrimary, IsAntialias = true, TextSize = size,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Segoe UI Emoji")
        };
        c.DrawText(text, cx, y, p);
    }

    public static void Progress(SKCanvas c, float x, float y, float w, float h, float pct, SKColor color)
    {
        RoundRect(c, x, y, w, h, h / 2, Card);
        if (pct > 0) RoundRect(c, x, y, w * Math.Clamp(pct, 0, 1), h, h / 2, color);
    }

    public static void StatusBar(SKCanvas c, float x, float y, float w)
    {
        Text(c, "12:34", x + 20, y + 25, 14, TextSecondary, true);
        Text(c, "📶  🔋", x + w - 90, y + 25, 13, TextSecondary);
    }

    public static void TabBar(SKCanvas c, float x, float y, float w, int active, string[] icons, string[] labels, SKColor accent)
    {
        RoundRect(c, x, y, w, 60, 0, Surface);
        using var bp = new SKPaint { Color = Border, StrokeWidth = 1, IsAntialias = true };
        c.DrawLine(x, y, x + w, y, bp);
        int n = icons.Length;
        float tabW = w / n;
        for (int i = 0; i < n; i++)
        {
            float tx = x + i * tabW + tabW / 2;
            var clr = i == active ? accent : TextMuted;
            TextC(c, icons[i], tx, y + 28, 20);
            TextC(c, labels[i], tx, y + 48, 10, clr);
        }
    }

    public static void StatItem(SKCanvas c, float x, float y, string label, string value, SKColor vc)
    {
        Text(c, label, x, y + 14, 12, TextSecondary);
        Text(c, value, x, y + 34, 20, vc, true);
    }

    public static void GradientBg(SKCanvas c, int w, int h, SKColor from, SKColor to)
    {
        using var p = new SKPaint { IsAntialias = true };
        using var s = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(w, h), [from, to], SKShaderTileMode.Clamp);
        p.Shader = s;
        c.DrawRect(0, 0, w, h, p);
    }

    public static void IconBg(SKCanvas c, float x, float y, float size, SKColor c1, SKColor c2)
    {
        using var p = new SKPaint { IsAntialias = true };
        using var s = SKShader.CreateLinearGradient(new SKPoint(x, y), new SKPoint(x + size, y + size), [c1, c2], SKShaderTileMode.Clamp);
        p.Shader = s;
        c.DrawRoundRect(new SKRect(x, y, x + size, y + size), size * 0.22f, size * 0.22f, p);
    }

    public static void FeatureGraphicBase(SKCanvas c, int w, int h, SKColor accent, Action<SKCanvas, float, float, float> drawIcon,
        string title1, string title2, string subtitle, (string emoji, SKColor color, float x, float y, float size)[] decoIcons)
    {
        GradientBg(c, w, h, Bg, accent);
        using var dp = new SKPaint { Color = accent.WithAlpha(30), IsAntialias = true };
        c.DrawCircle(w * 0.85f, h * 0.3f, 180, dp);
        c.DrawCircle(w * 0.75f, h * 0.7f, 120, dp);
        c.DrawCircle(w * 0.15f, h * 0.8f, 100, dp);

        float iconSize = 192;
        drawIcon(c, 60, (h - iconSize) / 2f, iconSize);

        Text(c, title1, 60 + iconSize + 40, h / 2f - 10, 52, TextPrimary, true);
        Text(c, title2, 60 + iconSize + 40, h / 2f + 50, 52, TextPrimary, true);
        Text(c, subtitle, 60 + iconSize + 40, h / 2f + 90, 22, TextSecondary);

        foreach (var (emoji, color, dx, dy, sz) in decoIcons)
        {
            Circle(c, dx + sz / 2, dy + sz / 2, sz / 2, color.WithAlpha(40));
            using var bp = new SKPaint { Color = color.WithAlpha(80), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
            c.DrawCircle(dx + sz / 2, dy + sz / 2, sz / 2, bp);
            TextC(c, emoji, dx + sz / 2, dy + sz / 2 + sz * 0.15f, sz * 0.45f);
        }
    }

    public static void GenerateScreenshot(int w, int h, string filename, string bannerText, Action<SKCanvas, SKRect> drawMockup, SKColor accent)
    {
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        var c = surface.Canvas;

        using var bgP = new SKPaint { IsAntialias = true };
        using var bgS = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, h),
            [SKColor.Parse("#1a1040"), Bg], SKShaderTileMode.Clamp);
        bgP.Shader = bgS;
        c.DrawRect(0, 0, w, h, bgP);

        using var dp = new SKPaint { Color = accent.WithAlpha(20), IsAntialias = true };
        c.DrawCircle(w * 0.8f, h * 0.1f, 200, dp);
        c.DrawCircle(w * 0.2f, h * 0.15f, 150, dp);

        float bY = 180;
        float bSize = w > 1100 ? 68 : 72;
        float bSpacing = w > 1100 ? 80 : 85;
        using var bP = new SKPaint
        {
            Color = TextPrimary, IsAntialias = true, TextSize = bSize,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            TextAlign = SKTextAlign.Center
        };
        foreach (var line in bannerText.Split('\n')) { c.DrawText(line, w / 2f, bY, bP); bY += bSpacing; }

        using var lP = new SKPaint { Color = accent.WithAlpha(120), IsAntialias = true, StrokeWidth = 3 };
        c.DrawLine(w / 2f - 120, bY + 10, w / 2f + 120, bY + 10, lP);

        float mX = 50, mY = bY + 50, mW = w - 100, mH = h - mY - 80;
        using var fP = new SKPaint { Color = SKColor.Parse("#2a2a4a"), IsAntialias = true };
        c.DrawRoundRect(new SKRect(mX - 6, mY - 6, mX + mW + 6, mY + mH + 6), 28, 28, fP);
        using var mBg = new SKPaint { Color = Bg, IsAntialias = true };
        var mRect = new SKRect(mX, mY, mX + mW, mY + mH);
        c.DrawRoundRect(mRect, 22, 22, mBg);

        c.Save();
        c.ClipRoundRect(new SKRoundRect(mRect, 22, 22));
        drawMockup(c, mRect);
        c.Restore();

        SavePng(surface, filename);
    }

    public static void SavePng(SKSurface surface, string filename)
    {
        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        var path = Path.Combine(OutputDir, filename);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
    }
}
