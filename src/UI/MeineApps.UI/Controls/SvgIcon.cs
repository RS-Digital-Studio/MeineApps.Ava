using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Markup.Xaml;
using System.Collections.Concurrent;

namespace MeineApps.UI.Controls;

/// <summary>
/// Leichtgewichtiges SVG-Icon-Control fuer geteilte App-Icons.
/// Laedt die <see cref="StreamGeometry"/> aus dem zentralen ResourceDictionary
/// <c>avares://MeineApps.UI/Assets/Icons/AppIcons.axaml</c> via <c>Icon_{Kind}</c>-Key.
///
/// <para>Konvention (siehe MeineApps.UI/CLAUDE.md):
/// Keine Unicode-Pfeile/-Sterne (uppercase u25BC u25B2 u2605) in UI-Text. Stattdessen entweder
/// Material.Icons, app-spezifische GameIcon (BomberBlast/RebornSaga) oder dieses Control.</para>
///
/// <para>Verwendung:</para>
/// <code>
/// &lt;ui:SvgIcon Kind="ChevronDown" Width="16" Height="16" Foreground="..."/&gt;
/// </code>
/// </summary>
public sealed class SvgIcon : ContentControl
{
    // Statischer Cache der ResourceDictionary — wird nur einmal geladen.
    private static ResourceDictionary? _iconResources;
    private static readonly object _initLock = new();

    /// <summary>Name des Icons (entspricht Key <c>Icon_{Kind}</c> im AppIcons.axaml).</summary>
    public static readonly StyledProperty<string> KindProperty =
        AvaloniaProperty.Register<SvgIcon, string>(nameof(Kind));

    /// <summary>Override-Geometry — wenn gesetzt wird Kind ignoriert.</summary>
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<SvgIcon, Geometry?>(nameof(Data));

    public string Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    static SvgIcon()
    {
        KindProperty.Changed.AddClassHandler<SvgIcon>((icon, e) => icon.OnKindChanged(e.NewValue as string));
        DataProperty.Changed.AddClassHandler<SvgIcon>((icon, _) => icon.UpdatePath());
        ForegroundProperty.Changed.AddClassHandler<SvgIcon>((icon, _) => icon.UpdatePath());
    }

    public SvgIcon()
    {
        Width = 24;
        Height = 24;
    }

    private static ResourceDictionary LoadIconResources()
    {
        if (_iconResources != null) return _iconResources;
        lock (_initLock)
        {
            if (_iconResources != null) return _iconResources;
            var uri = new System.Uri("avares://MeineApps.UI/Assets/Icons/AppIcons.axaml");
            _iconResources = (ResourceDictionary)AvaloniaXamlLoader.Load(uri);
            return _iconResources;
        }
    }

    private void OnKindChanged(string? newKind)
    {
        if (string.IsNullOrEmpty(newKind))
        {
            Data = null;
            return;
        }

        // Resource-Lookup: AppIcons.axaml liefert StreamGeometry unter Icon_{Kind}
        var key = $"Icon_{newKind}";
        var resources = LoadIconResources();
        if (resources.TryGetResource(key, null, out var resource) && resource is Geometry geo)
        {
            Data = geo;
        }
    }

    private void UpdatePath()
    {
        var geometry = Data;
        if (geometry == null)
        {
            Content = null;
            return;
        }

        // ViewBox-aequivalent: 24x24-Path skaliert auf Width/Height via Stretch=Uniform
        Content = new Viewbox
        {
            Stretch = Stretch.Uniform,
            Child = new Avalonia.Controls.Shapes.Path
            {
                Data = geometry,
                Fill = Foreground ?? Brushes.Black,
            }
        };
    }
}
