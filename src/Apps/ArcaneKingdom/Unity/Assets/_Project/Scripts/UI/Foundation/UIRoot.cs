#nullable enable
using ArcaneKingdom.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Foundation
{
    /// <summary>
    /// MonoBehaviour das in der Boot-Scene auf einem GameObject (z.B. [UI]) liegt
    /// und das UIDocument haelt.
    ///
    /// <para><b>Lifecycle:</b></para>
    /// <para>
    /// In Awake werden ScreenContainer + OverlayContainer angelegt — bewusst NICHT an
    /// UIDocument.rootVisualElement gehängt, weil das in Awake noch null ist. Stattdessen
    /// bekommt der ScreenManager bereits eine stabile VisualElement-Referenz.
    /// </para>
    /// <para>
    /// In OnEnable (wenn UIDocument bereit ist) werden die Container an
    /// rootVisualElement gehängt + das Theme verlinkt. Das ist idempotent — bei
    /// Domain-Reload / Scene-Switch wird nichts doppelt eingehängt.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [RequireComponent(typeof(UIDocument))]
    public sealed class UIRoot : MonoBehaviour
    {
        [SerializeField] private StyleSheet? themeStyleSheet;

        private UIDocument? _document;
        private VisualElement _screenContainer = null!;
        private VisualElement _overlayContainer = null!;
        private bool _attachedToRoot;

        /// <summary>Root-Container für Screens.</summary>
        public VisualElement ScreenContainer => _screenContainer;

        /// <summary>Overlay-Container — liegt UEBER ScreenContainer (Toasts, Loading-Spinner).</summary>
        public VisualElement OverlayContainer => _overlayContainer;

        /// <summary>True wenn Container an UIDocument-Root angehangen sind UND Theme-StyleSheet
        /// in der Parent-Chain ist. Erst dann sind UXML-var()-Variablen auflösbar.</summary>
        public bool IsReady => _attachedToRoot;

        private void Awake()
        {
            // Container in Awake bauen — gibt dem ScreenManager im DI-Build eine
            // stabile Referenz, auch wenn UIDocument.rootVisualElement noch null ist.
            BuildContainers();

            if (transform.parent != null)
            {
                GameLogger.Verbose("UI", "UIRoot war Child — fuer DontDestroyOnLoad zum Root gemacht.");
                transform.SetParent(null, worldPositionStays: false);
            }
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            // UIDocument.rootVisualElement ist ab OnEnable verfügbar.
            // Container an root haengen wenn noch nicht passiert.
            AttachToRoot();
        }

        private void BuildContainers()
        {
            if (_screenContainer != null) return;

            _screenContainer = new VisualElement { name = "screen-container" };
            _screenContainer.style.flexGrow = 1;
            _screenContainer.style.position = Position.Relative;

            _overlayContainer = new VisualElement { name = "overlay-container" };
            _overlayContainer.style.position = Position.Absolute;
            _overlayContainer.style.left = 0;
            _overlayContainer.style.right = 0;
            _overlayContainer.style.top = 0;
            _overlayContainer.style.bottom = 0;
            _overlayContainer.pickingMode = PickingMode.Ignore;
        }

        private void AttachToRoot()
        {
            if (_attachedToRoot) return;

            _document ??= GetComponent<UIDocument>();
            if (_document == null)
            {
                GameLogger.Error("UI", "UIRoot ohne UIDocument-Component — Container bleiben detached.");
                return;
            }

            var root = _document.rootVisualElement;
            if (root == null)
            {
                GameLogger.Warning("UI",
                    "UIDocument.rootVisualElement in OnEnable noch null — versuche es im naechsten Frame.");
                StartCoroutine(WaitForRootAndAttach());
                return;
            }

            DoAttach(root);
        }

        private System.Collections.IEnumerator WaitForRootAndAttach()
        {
            while (_document != null && _document.rootVisualElement == null)
                yield return null;
            if (_document?.rootVisualElement is { } root)
                DoAttach(root);
        }

        private void DoAttach(VisualElement root)
        {
            if (themeStyleSheet != null && !root.styleSheets.Contains(themeStyleSheet))
                root.styleSheets.Add(themeStyleSheet);

            root.AddToClassList("ak-root");
            root.style.flexGrow = 1;

            if (_screenContainer.parent != root) root.Add(_screenContainer);
            if (_overlayContainer.parent != root) root.Add(_overlayContainer);

            _attachedToRoot = true;
            GameLogger.Info("UI",
                $"UIRoot bereit — Containers an UIDocument-Root angehangen " +
                $"(root.childCount={root.childCount}, theme={(themeStyleSheet != null ? "ja" : "nein")}).");
        }
    }
}
