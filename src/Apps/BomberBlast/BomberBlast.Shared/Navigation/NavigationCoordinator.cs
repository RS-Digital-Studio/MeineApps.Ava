using BomberBlast.ViewModels;

namespace BomberBlast.Navigation;

/// <summary>
/// Default-Implementation von <see cref="INavigationCoordinator"/>.
///
/// <para>
/// Leeres Geruest. Die Routing-Logik wird noch aus
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
            "Routing-Logik liegt aktuell noch in MainViewModel.NavigateToRouteAsync — wird hier hin migriert.");

    public void NavigateTo(NavigationRequest request)
        => throw new NotImplementedException("Migration aus MainViewModel ausstehend.");

    public void HideAll()
        => throw new NotImplementedException("Migration aus MainViewModel ausstehend.");

    /// <summary>Helper damit die Subscription-Verkabelung waehrend der Migration nicht crasht.</summary>
    internal void SetActiveView(ActiveView view)
    {
        if (_activeView == view) return;
        _activeView = view;
        ActiveViewChanged?.Invoke(view);
    }
}
