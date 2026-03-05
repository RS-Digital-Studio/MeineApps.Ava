using Avalonia;
using Avalonia.Controls;

namespace HandwerkerImperium.Icons;

/// <summary>
/// Custom Icon-Control fuer HandwerkerImperium (Warme Werkstatt Stil).
/// Erbt von PathIcon fuer native Avalonia-Performance.
/// </summary>
public class GameIcon : PathIcon
{
    public static readonly StyledProperty<GameIconKind> KindProperty =
        AvaloniaProperty.Register<GameIcon, GameIconKind>(nameof(Kind));

    public GameIconKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    static GameIcon()
    {
        KindProperty.Changed.AddClassHandler<GameIcon>((icon, _) => icon.UpdateGeometry());
    }

    public GameIcon()
    {
        Width = 24;
        Height = 24;
    }

    private void UpdateGeometry()
    {
        Data = GameIconPaths.GetGeometry(Kind);
    }
}
