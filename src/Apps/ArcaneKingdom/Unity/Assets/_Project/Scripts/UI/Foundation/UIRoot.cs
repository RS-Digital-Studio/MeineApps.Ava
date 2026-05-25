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
    /// Wird vom <see cref="UIBootstrap"/> in den DI-Container per
    /// RegisterComponent injiziert, damit der ScreenManager den Root bekommt.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class UIRoot : MonoBehaviour
    {
        [SerializeField] private StyleSheet? themeStyleSheet;

        private UIDocument? _document;

        /// <summary>Root-Container fuer Screens. Wird vom ScreenManager befuellt.</summary>
        public VisualElement ScreenContainer { get; private set; } = null!;

        /// <summary>Overlay-Container — liegt UEBER ScreenContainer (Toasts, Loading-Spinner).</summary>
        public VisualElement OverlayContainer { get; private set; } = null!;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            var root = _document.rootVisualElement;

            if (root == null)
            {
                GameLogger.Error("UI", "UIDocument.rootVisualElement ist null beim Awake.");
                return;
            }

            // Theme zuweisen wenn im Inspector verlinkt
            if (themeStyleSheet != null)
                root.styleSheets.Add(themeStyleSheet);

            root.AddToClassList("ak-root");
            root.style.flexGrow = 1;

            // Container fuer Screens
            ScreenContainer = new VisualElement { name = "screen-container" };
            ScreenContainer.style.flexGrow = 1;
            ScreenContainer.style.position = Position.Relative;
            root.Add(ScreenContainer);

            // Container fuer Overlays (Toasts, Loading, immer obendrueber)
            OverlayContainer = new VisualElement { name = "overlay-container" };
            OverlayContainer.style.position = Position.Absolute;
            OverlayContainer.style.left = 0;
            OverlayContainer.style.right = 0;
            OverlayContainer.style.top = 0;
            OverlayContainer.style.bottom = 0;
            OverlayContainer.pickingMode = PickingMode.Ignore; // standardmaessig nicht klickbar
            root.Add(OverlayContainer);

            DontDestroyOnLoad(gameObject);
            GameLogger.Info("UI", "UIRoot bereit (ScreenContainer + OverlayContainer).");
        }
    }
}
