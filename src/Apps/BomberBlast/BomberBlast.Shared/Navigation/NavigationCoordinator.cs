using BomberBlast.ViewModels;

namespace BomberBlast.Navigation;

/// <summary>
/// Default-Implementation von <see cref="INavigationCoordinator"/> (Welle 6 MainViewModel-Refactor).
///
/// <para>
/// Phase 1: Leeres Geruest. Die Logik wird in Phase 5 (NavigationCoordinator-Migration) aus
/// <see cref="MainViewModel.NavigateToRouteAsync"/> hier hin verschoben.
/// </para>
/// </summary>
public sealed class NavigationCoordinator : INavigationCoordinator
{
    private ActiveView _activeView;

    public ActiveView ActiveView => _activeView;

    public event Action<ActiveView>? ActiveViewChanged;

    public Task NavigateToRouteAsync(string route)
        => throw new NotImplementedException(
            "NavigationCoordinator wird in Phase 5 mit Logik gefuellt. " +
            "Bis dahin haelt MainViewModel die Routing-Logik selbst.");

    public void NavigateTo(NavigationRequest request)
        => throw new NotImplementedException(
            "NavigationCoordinator wird in Phase 5 mit Logik gefuellt.");

    public void HideAll()
        => throw new NotImplementedException(
            "NavigationCoordinator wird in Phase 5 mit Logik gefuellt.");

    /// <summary>Helper damit die Subscription-Verkabelung waehrend der Migration nicht crasht.</summary>
    internal void SetActiveView(ActiveView view)
    {
        if (_activeView == view) return;
        _activeView = view;
        ActiveViewChanged?.Invoke(view);
    }
}
