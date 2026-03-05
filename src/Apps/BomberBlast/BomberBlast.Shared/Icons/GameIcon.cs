using Avalonia;
using Avalonia.Controls;

namespace BomberBlast.Icons;

/// <summary>
/// Custom Icon-Control fuer BomberBlast (Neon Arcade Stil).
/// Erbt von PathIcon fuer native Avalonia-Performance.
/// Verwendung identisch zu MaterialIcon:
/// <![CDATA[
///   <icons:GameIcon Kind="Sword" Foreground="White" Width="24" Height="24" />
/// ]]>
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
        // Standard-Groesse wie MaterialIcon
        Width = 24;
        Height = 24;
    }

    private void UpdateGeometry()
    {
        Data = GameIconPaths.GetGeometry(Kind);
    }
}
