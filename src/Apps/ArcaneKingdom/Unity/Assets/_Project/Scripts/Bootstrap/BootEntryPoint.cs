#nullable enable
using ArcaneKingdom.Core.Utility;
using VContainer.Unity;

namespace ArcaneKingdom.Bootstrap
{
    /// <summary>
    /// Wird einmalig beim App-Start aufgerufen, sobald der DI-Container gebaut ist.
    /// Loggt Status und uebergibt die Kontrolle an den Login-Flow.
    /// </summary>
    public sealed class BootEntryPoint : IStartable
    {
        public void Start()
        {
            GameLogger.Info("Boot", "ArcaneKingdom gestartet.");
            // TODO: Login-Scene additive laden, Splash-Screen ausblenden.
        }
    }
}
