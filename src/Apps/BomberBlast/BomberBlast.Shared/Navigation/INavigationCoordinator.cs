using BomberBlast.ViewModels;

namespace BomberBlast.Navigation;

/// <summary>
/// Koordiniert die Top-Level-Navigation der App (NavigateToRouteAsync, NavigateTo(NavigationRequest)).
///
/// <para>
/// Source-of-Truth fuer <see cref="ActiveView"/>. MainViewModel forwarded den Wert nur noch
/// per Subscription auf <see cref="ActiveViewChanged"/> — alle bestehenden AXAML-Bindings
/// (<c>{Binding ActiveView, Converter=..., ConverterParameter=Game}</c>) bleiben kompatibel.
/// </para>
///
/// <para>
/// CloudSave-Init-Race-Guard: NavigateToRouteAsync awaitet (mit 3s-Cap) den im
/// <see cref="LifecycleHub"/> gestarteten Init-Task bevor Routen wie "game"/"levelselect"/"dungeon"
/// freigegeben werden — verhindert dass der lokale Leer-State beim Erstlogin auf einem neuen
/// Geraet ueber den noch nicht geladenen Cloud-State geschoben wird.
/// </para>
/// </summary>
public interface INavigationCoordinator
{
    /// <summary>Aktuelle View (Source-of-Truth). Aenderungen feuern <see cref="ActiveViewChanged"/>.</summary>
    ActiveView ActiveView { get; }

    /// <summary>Wird gefeuert wenn <see cref="ActiveView"/> sich aendert.</summary>
    event Action<ActiveView>? ActiveViewChanged;

    /// <summary>String-basierte Navigation (Legacy + Deep-Links). Routes wie "menu", "game", "shop", "game?mode=story&amp;level=5".</summary>
    Task NavigateToRouteAsync(string route);

    /// <summary>Typsichere Navigation. Delegiert intern an <see cref="NavigateToRouteAsync"/> via NavigationRouteMapper.</summary>
    void NavigateTo(NavigationRequest request);

    /// <summary>Versteckt alle Views (HideAll). Wird vor jedem View-Wechsel aufgerufen.</summary>
    void HideAll();
}
