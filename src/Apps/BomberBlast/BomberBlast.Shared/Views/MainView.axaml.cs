using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using BomberBlast.Graphics;
using BomberBlast.ViewModels;
using MeineApps.UI.SkiaSharp.Shaders;
using System.Threading.Tasks;

namespace BomberBlast.Views;

public partial class MainView : UserControl
{
    private MainViewModel? _vm;
    private readonly Random _rng = new();

    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Altes ViewModel abmelden
        if (_vm != null)
        {
            _vm.FloatingTextRequested -= OnFloatingText;
            _vm.CelebrationRequested -= OnCelebration;
        }

        _vm = DataContext as MainViewModel;

        // Neues ViewModel anmelden — kein FindControl/UpdateActiveClasses mehr noetig:
        // v2.0.37 setzt Classes.Active per ActiveView-Binding + ActiveViewEqualsConverter direkt im XAML.
        if (_vm != null)
        {
            _vm.FloatingTextRequested += OnFloatingText;
            _vm.CelebrationRequested += OnCelebration;
        }
    }

    private void OnFloatingText(string text, string category)
    {
        var color = category switch
        {
            "success" => Color.Parse("#22C55E"),
            "gold" => Color.Parse("#FFD700"),
            "error" => Color.Parse("#EF4444"),
            _ => Color.Parse("#3B82F6")
        };

        var w = FloatingTextCanvas.Bounds.Width;
        if (w < 10) w = 300;
        var h = FloatingTextCanvas.Bounds.Height;
        if (h < 10) h = 400;

        FloatingTextCanvas.ShowFloatingText(text, w * (0.2 + _rng.NextDouble() * 0.6), h * 0.35, color, 20);
    }

    private void OnCelebration()
    {
        CelebrationCanvas.ShowConfetti();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Preload-Pipeline: Shader + Renderer vorkompilieren während Splash angezeigt wird.
        // Audit L12: Granularere Progress-Reports verhindern "Freeze"-Eindruck zwischen Steps.
        Splash.PreloadAction = async (reportProgress) =>
        {
            // HINWEIS: Die SkSL-GPU-Shader (ShaderPreloader.PreloadAll) UND ExplosionShaders werden
            // bereits von der BomberBlastLoadingPipeline (App.RunLoadingAsync) kompiliert. Hier NICHT
            // erneut aufrufen — sonst doppelte GPU-Shader-Kompilierung auf konkurrierenden Threads
            // (600-2400ms verschenkt) plus Race auf die nicht-atomaren ??=-Effekt-Caches
            // (verwaiste SKRuntimeEffect-Allokation = nativer Leak). Hier nur die statischen
            // Renderer-Klassen, die die Pipeline NICHT abdeckt — SKPaint/SKFont/SKMaskFilter/SKPath
            // vorallokieren, verhindert Jank beim ersten Frame/View-Öffnen.
            reportProgress(0.1f, "Effekte werden geladen...");
            await Task.Run(() => { HelpIconRenderer.Preload(); });
            reportProgress(0.4f, "Icons werden geladen...");
            await Task.Run(() => { TornMetalRenderer.Preload(); });
            reportProgress(0.6f, "Buttons werden geladen...");
            await Task.Run(() => { RarityRenderer.Preload(); });
            reportProgress(0.85f, "Kartensystem wird geladen...");
            await Task.Run(() => { MenuBackgroundRenderer.Preload(); });

            reportProgress(1.0f, "Fertig");
        };
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Events abmelden bei Detach (verhindert Memory Leaks)
        if (_vm != null)
        {
            _vm.FloatingTextRequested -= OnFloatingText;
            _vm.CelebrationRequested -= OnCelebration;
            _vm = null;
        }

        DataContextChanged -= OnDataContextChanged;
    }
}
