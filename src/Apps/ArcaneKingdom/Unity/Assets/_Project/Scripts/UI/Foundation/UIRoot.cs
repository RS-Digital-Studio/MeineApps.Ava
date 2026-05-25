#nullable enable
using ArcaneKingdom.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Foundation
{
    /// <summary>
    /// MonoBehaviour das in der Boot-Scene auf einem GameObject (z.B. [UI]) liegt
    /// und das UIDocument haelt. Bietet Zugriff auf den ScreenRoot-Container an den
    /// der ScreenManager seine Screens haengt.
    ///
    /// Wird vom <see cref="UIInstaller"/> ueber RegisterComponent in den DI-Container
    /// gehangen, damit der ScreenManager den Root bekommt.
    ///
    /// <para>
    /// <b>DefaultExecutionOrder(-10000):</b> UIRoot.Awake muss VOR LifetimeScope.Awake
    /// laufen, sonst ist ScreenContainer noch null wenn der ScreenManager im DI gebaut
    /// wird. -10000 ist niedriger als VContainer's LifetimeScope default (-50).
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [RequireComponent(typeof(UIDocument))]
    public sealed class UIRoot : MonoBehaviour
    {
        [SerializeField] private StyleSheet? themeStyleSheet;

        private UIDocument? _document;
        private VisualElement? _screenContainer;
        private VisualElement? _overlayContainer;

        /// <summary>Root-Container fuer Screens. Lazy-initialisiert.</summary>
        public VisualElement ScreenContainer
        {
            get
            {
                EnsureInitialized();
                return _screenContainer!;
            }
        }

        /// <summary>Overlay-Container — liegt UEBER ScreenContainer (Toasts, Loading-Spinner).</summary>
        public VisualElement OverlayContainer
        {
            get
            {
                EnsureInitialized();
                return _overlayContainer!;
            }
        }

        private void Awake()
        {
            EnsureInitialized();

            // DontDestroyOnLoad funktioniert nur an Root-GameObjects.
            // Wenn [UI] Child von [Bootstrapper] ist, erst zum Root machen.
            if (transform.parent != null)
            {
                GameLogger.Verbose("UI", "UIRoot war Child — fuer DontDestroyOnLoad zum Root gemacht.");
                transform.SetParent(null, worldPositionStays: false);
            }
            DontDestroyOnLoad(gameObject);
        }

        private void EnsureInitialized()
        {
            if (_screenContainer != null) return;

            _document ??= GetComponent<UIDocument>();
            if (_document == null)
            {
                GameLogger.Error("UI", "UIRoot ohne UIDocument-Component — kann ScreenContainer nicht bauen.");
                _screenContainer = new VisualElement { name = "screen-container-fallback" };
                _overlayContainer = new VisualElement { name = "overlay-container-fallback" };
                return;
            }

            var root = _document.rootVisualElement;
            if (root == null)
            {
                GameLogger.Warning("UI",
                    "UIDocument.rootVisualElement noch null — Container werden detached angelegt " +
                    "und beim naechsten EnsureInitialized angehaengt.");
                _screenContainer = new VisualElement { name = "screen-container" };
                _overlayContainer = new VisualElement { name = "overlay-container" };
                return;
            }

            // Theme zuweisen wenn im Inspector verlinkt
            if (themeStyleSheet != null && !root.styleSheets.Contains(themeStyleSheet))
                root.styleSheets.Add(themeStyleSheet);

            root.AddToClassList("ak-root");
            root.style.flexGrow = 1;

            // Container fuer Screens
            _screenContainer = new VisualElement { name = "screen-container" };
            _screenContainer.style.flexGrow = 1;
            _screenContainer.style.position = Position.Relative;
            root.Add(_screenContainer);

            // Container fuer Overlays (Toasts, Loading, immer obendrueber)
            _overlayContainer = new VisualElement { name = "overlay-container" };
            _overlayContainer.style.position = Position.Absolute;
            _overlayContainer.style.left = 0;
            _overlayContainer.style.right = 0;
            _overlayContainer.style.top = 0;
            _overlayContainer.style.bottom = 0;
            _overlayContainer.pickingMode = PickingMode.Ignore;
            root.Add(_overlayContainer);

            GameLogger.Info("UI", "UIRoot bereit (ScreenContainer + OverlayContainer).");
        }
    }
}
