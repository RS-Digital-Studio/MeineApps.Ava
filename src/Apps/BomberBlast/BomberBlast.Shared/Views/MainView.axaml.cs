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

        // Preload-Pipeline: Shader + Renderer vorkompilieren während Splash angezeigt wird
        Splash.PreloadAction = async (reportProgress) =>
        {
            // Schritt 1: SkSL-GPU-Shader kompilieren (12 Shader, 600-2400ms auf Android)
            reportProgress(0.0f, "Grafik-Engine wird vorbereitet...");
            await Task.Run(() => ShaderPreloader.PreloadAll());

            // Schritt 2: Statische Renderer-Klassen initialisieren
            // (SKPaint/SKFont/SKMaskFilter/SKPath + Noise-LUT vorallokieren,
            //  verhindert Jank beim ersten Frame/View-Öffnen)
            reportProgress(0.7f, "Effekte werden geladen...");
            await Task.Run(() =>
            {
                ExplosionShaders.Preload();
                HelpIconRenderer.Preload();
                TornMetalRenderer.Preload();
                RarityRenderer.Preload();
                MenuBackgroundRenderer.Preload();
            });

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
