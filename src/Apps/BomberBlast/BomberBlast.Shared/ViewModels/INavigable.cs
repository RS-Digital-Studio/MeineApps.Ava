namespace BomberBlast.ViewModels;

/// <summary>
/// Interface für ViewModels mit Navigation (ersetzt Reflection-basiertes WireNavigation)
/// </summary>
public interface INavigable
{
    event Action<NavigationRequest>? NavigationRequested;
}
