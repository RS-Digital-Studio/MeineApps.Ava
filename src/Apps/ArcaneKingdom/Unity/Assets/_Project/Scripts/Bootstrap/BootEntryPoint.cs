#nullable enable
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using VContainer.Unity;

namespace ArcaneKingdom.Bootstrap
{
    /// <summary>
    /// Wird einmalig nach dem DI-Container-Build aufgerufen. Setzt den initialen
    /// Screen (Login) und uebergibt damit die Kontrolle an das UI-Layer.
    /// </summary>
    public sealed class BootEntryPoint : IStartable
    {
        private readonly ScreenManager _screenManager;

        public BootEntryPoint(ScreenManager screenManager)
        {
            _screenManager = screenManager;
        }

        public void Start()
        {
            GameLogger.Info("Boot", "ArcaneKingdom gestartet — Initial-Screen: Login.");
            // ReplaceAsync feuert OnEnterAsync des Login-Screens, der dann den
            // LoginController triggert und Status-Updates an den User zeigt.
            _screenManager.ReplaceAsync(ScreenId.Login).Forget();
        }
    }
}
