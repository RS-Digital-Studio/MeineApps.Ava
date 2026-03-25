using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace GardenControl.Shared.Views;

public partial class DashboardView : UserControl
{
    /// <summary>Pumpe aktiv → Hintergrund-Brush (Blau-Gradient aktiv, dunkelgrau inaktiv)</summary>
    public static readonly FuncValueConverter<bool, IBrush> PumpBgConverter =
        new(active =>
        {
            if (active == true)
            {
                var brush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative)
                };
                brush.GradientStops.Add(new GradientStop(Color.Parse("#1565C0"), 0));
                brush.GradientStops.Add(new GradientStop(Color.Parse("#42A5F5"), 1));
                return brush;
            }
            return new SolidColorBrush(Color.Parse("#2A3A4E"));
        });

    /// <summary>Pumpe aktiv → Farbe (Blau/Grau)</summary>
    public static readonly FuncValueConverter<bool, IBrush> PumpColorConverter =
        new(active => active == true
            ? new SolidColorBrush(Color.Parse("#42A5F5"))
            : new SolidColorBrush(Color.Parse("#5A7A96")));

    /// <summary>Pumpe aktiv → Text</summary>
    public static readonly FuncValueConverter<bool, string> PumpTextConverter =
        new(active => active == true ? "Pumpe aktiv" : "Pumpe aus");

    /// <summary>String "#RRGGBB" → SolidColorBrush</summary>
    public static readonly FuncValueConverter<string, IBrush> StringToBrush =
        new(color =>
        {
            if (string.IsNullOrEmpty(color)) return new SolidColorBrush(Color.Parse("#5A7A96"));
            try { return new SolidColorBrush(Color.Parse(color)); }
            catch { return new SolidColorBrush(Color.Parse("#5A7A96")); }
        });

    /// <summary>Modus-Button Hintergrund (vereinfacht)</summary>
    public static readonly FuncValueConverter<string, IBrush> ModeButtonConverter =
        new(_ => Brushes.Transparent);

    /// <summary>Schwellenwert → Margin für Markierung auf ProgressBar</summary>
    public static readonly FuncValueConverter<int, Thickness> ThresholdMarginConverter =
        new(percent =>
        {
            var left = Math.Clamp(percent, 0, 100) * 3;
            return new Thickness(left, 0, 0, 0);
        });

    /// <summary>Feuchtigkeitsprozent (0-100) → GridLength für Column-Breite</summary>
    public static readonly FuncValueConverter<double, GridLength> MoistureToGridLength =
        new(percent =>
        {
            var clamped = Math.Clamp(percent, 0, 100);
            return new GridLength(clamped, GridUnitType.Star);
        });

    /// <summary>Feuchtigkeitsprozent (0-100) → Rest-GridLength (100-Wert)</summary>
    public static readonly FuncValueConverter<double, GridLength> MoistureToRestGridLength =
        new(percent =>
        {
            var clamped = 100.0 - Math.Clamp(percent, 0, 100);
            return new GridLength(Math.Max(clamped, 0.01), GridUnitType.Star);
        });

    /// <summary>Wetter-Pause aktiv → Orange-Hintergrund</summary>
    public static readonly FuncValueConverter<bool, IBrush> PauseBgConverter =
        new(paused => paused == true
            ? new SolidColorBrush(Color.Parse("#E65100"))
            : new SolidColorBrush(Color.Parse("#1B5E20")));

    public DashboardView()
    {
        InitializeComponent();
    }
}
