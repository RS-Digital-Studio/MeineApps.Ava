using BomberBlast.Services;
using BomberBlast.ViewModels;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests fuer <see cref="ChildViewModelWiring.Wire"/>.
/// Validiert die Standard-Subscriptions: Navigation, FloatingText, Celebration.
/// </summary>
public class ChildViewModelWiringTests
{
    /// <summary>
    /// Fake-VM, das alle drei Optional-Interfaces implementiert — so testen wir alle drei
    /// Routing-Pfade an einer Instanz.
    /// </summary>
    private sealed class FakeVm : INavigable, IFloatingTextEmitter, ICelebrationEmitter
    {
        public event Action<NavigationRequest>? NavigationRequested;
        public event Action<string, string>? FloatingTextRequested;
        public event Action? CelebrationRequested;

        public void RaiseNavigation(NavigationRequest request) => NavigationRequested?.Invoke(request);
        public void RaiseFloatingText(string text, string style) => FloatingTextRequested?.Invoke(text, style);
        public void RaiseCelebration() => CelebrationRequested?.Invoke();
    }

    /// <summary>VM nur mit INavigable — ohne FloatingText/Celebration-Interfaces.</summary>
    private sealed class NavigationOnlyVm : INavigable
    {
        public event Action<NavigationRequest>? NavigationRequested;
        public void RaiseNavigation(NavigationRequest request) => NavigationRequested?.Invoke(request);
    }

    [Fact]
    public void Wire_NavigationRequested_LeitetAnCallbackWeiter()
    {
        var vm = new FakeVm();
        var eventBus = new GameEventBus();
        NavigationRequest? received = null;
        ChildViewModelWiring.Wire(vm, request => received = request, eventBus);

        vm.RaiseNavigation(new GoShop());

        received.Should().BeOfType<GoShop>();
    }

    [Fact]
    public void Wire_FloatingTextRequested_LeitetAnEventBusWeiter()
    {
        var vm = new FakeVm();
        var eventBus = new GameEventBus();
        string? capturedText = null;
        string? capturedStyle = null;
        eventBus.FloatingTextRequested += (t, s) => { capturedText = t; capturedStyle = s; };

        ChildViewModelWiring.Wire(vm, _ => { }, eventBus);
        vm.RaiseFloatingText("+100 Coins", "gold");

        capturedText.Should().Be("+100 Coins");
        capturedStyle.Should().Be("gold");
    }

    [Fact]
    public void Wire_CelebrationRequested_LeitetAnEventBusWeiter()
    {
        var vm = new FakeVm();
        var eventBus = new GameEventBus();
        var fired = 0;
        eventBus.CelebrationRequested += () => fired++;

        ChildViewModelWiring.Wire(vm, _ => { }, eventBus);
        vm.RaiseCelebration();
        vm.RaiseCelebration();

        fired.Should().Be(2);
    }

    [Fact]
    public void Wire_NavigationOnlyVm_LeitetTrotzdemNavigationWeiter()
    {
        var vm = new NavigationOnlyVm();
        var eventBus = new GameEventBus();
        NavigationRequest? received = null;
        ChildViewModelWiring.Wire(vm, request => received = request, eventBus);

        vm.RaiseNavigation(new GoBack());

        received.Should().BeOfType<GoBack>();
    }

    [Fact]
    public void Wire_NavigationOnlyVm_KeinFloatingOderCelebrationRouting()
    {
        // Kein Crash bei VMs ohne IFloatingTextEmitter / ICelebrationEmitter.
        var vm = new NavigationOnlyVm();
        var eventBus = new GameEventBus();
        var floatingFired = 0;
        var celebrationFired = 0;
        eventBus.FloatingTextRequested += (_, _) => floatingFired++;
        eventBus.CelebrationRequested += () => celebrationFired++;

        ChildViewModelWiring.Wire(vm, _ => { }, eventBus);
        // Nichts kann auf NavigationOnlyVm gefeuert werden — Test stellt sicher dass Wire nicht crasht.

        floatingFired.Should().Be(0);
        celebrationFired.Should().Be(0);
    }

    [Fact]
    public void Wire_MehrereVMs_AlleRoutenAnDenselbenCallback()
    {
        var vm1 = new FakeVm();
        var vm2 = new FakeVm();
        var eventBus = new GameEventBus();
        var navigations = new List<NavigationRequest>();
        ChildViewModelWiring.Wire(vm1, navigations.Add, eventBus);
        ChildViewModelWiring.Wire(vm2, navigations.Add, eventBus);

        vm1.RaiseNavigation(new GoShop());
        vm2.RaiseNavigation(new GoLeague());

        navigations.Should().HaveCount(2);
        navigations[0].Should().BeOfType<GoShop>();
        navigations[1].Should().BeOfType<GoLeague>();
    }

    [Fact]
    public void Wire_NavigationRequestedNull_WirdGefiltert()
    {
        // Wenn ein null-Payload via Reraise durchgereicht wird (defensive Guard im Wiring),
        // soll der onNavigate-Callback NICHT aufgerufen werden.
        var vm = new FakeVm();
        var eventBus = new GameEventBus();
        var fired = 0;
        ChildViewModelWiring.Wire(vm, _ => fired++, eventBus);

        vm.RaiseNavigation(null!);  // simuliert null-Payload (sollte gefiltert werden)

        fired.Should().Be(0);
    }

    [Fact]
    public void GameEventBus_RaiseFloatingText_FunktioniertOhneSubscriber()
    {
        // Sicherstellen dass null-Subscriber kein Crash erzeugt (Multicast-Delegate-Verhalten).
        var eventBus = new GameEventBus();
        var ex = Record.Exception(() => eventBus.RaiseFloatingText("x", "y"));
        ex.Should().BeNull();
    }
}
