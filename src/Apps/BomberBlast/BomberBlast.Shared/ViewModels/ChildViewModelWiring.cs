using BomberBlast.Services;

namespace BomberBlast.ViewModels;

/// <summary>
/// Statische Helper-Klasse fuer die Standard-Subscriptions, die ein Child-VM braucht:
/// Navigation, FloatingText (via <see cref="IFloatingTextEmitter"/>), Celebration
/// (via <see cref="ICelebrationEmitter"/>).
///
/// <para>
/// Wird von <see cref="ChildViewModelRegistry.WireCommon"/> aufgerufen und isoliert
/// fuer Unit-Tests genutzt — kein DI-Aufbau noetig.
/// </para>
/// </summary>
public static class ChildViewModelWiring
{
    /// <summary>
    /// Verdrahtet die Standard-Events eines VMs:
    /// </summary>
    /// <param name="vm">Das Child-VM (immer <see cref="INavigable"/>).</param>
    /// <param name="onNavigate">Callback fuer <see cref="INavigable.NavigationRequested"/> — typischerweise routet auf den Compositor.</param>
    /// <param name="eventBus">EventBus fuer FloatingText- und Celebration-Forwarding.</param>
    public static void Wire(INavigable vm, Action<NavigationRequest> onNavigate, IGameEventBus eventBus)
    {
        vm.NavigationRequested += request =>
        {
            if (request is not null) onNavigate(request);
        };
        if (vm is IFloatingTextEmitter floatingEmitter)
            floatingEmitter.FloatingTextRequested += (text, style) => eventBus.RaiseFloatingText(text, style);
        if (vm is ICelebrationEmitter celebrationEmitter)
            celebrationEmitter.CelebrationRequested += () => eventBus.RaiseCelebration();
    }
}
