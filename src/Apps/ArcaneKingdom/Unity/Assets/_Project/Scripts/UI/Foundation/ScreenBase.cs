#nullable enable
using System.Threading;
using ArcaneKingdom.Core.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Foundation
{
    /// <summary>
    /// Basis fuer Screens — laedt das UXML-Asset aus Resources und biettet Helper
    /// fuer Element-Lookup mit klaren Fehlermeldungen.
    ///
    /// Konkrete Screens (z.B. <c>HubScreen</c>) erben hiervon, ueberschreiben
    /// <see cref="UxmlPath"/>, <see cref="BindElements"/>, optional
    /// <see cref="OnEnterAsync"/> und <see cref="OnLeaveAsync"/>.
    /// </summary>
    public abstract class ScreenBase : IScreen
    {
        public abstract string Id { get; }
        public virtual bool IsOverlay => false;

        /// <summary>
        /// Resources-Pfad zur UXML-Datei OHNE Endung, relativ zu Assets/_Project/Resources/.
        /// Beispiel: "UI/HubScreen" laedt Assets/_Project/Resources/UI/HubScreen.uxml.
        /// </summary>
        protected abstract string UxmlPath { get; }

        private VisualElement? _root;
        public VisualElement Root
            => _root ?? throw new System.InvalidOperationException(
                $"Screen '{Id}' nicht gebaut. Build() muss vor Root-Zugriff aufgerufen werden.");

        public void Build()
        {
            if (_root != null) return; // Idempotent

            var asset = Resources.Load<VisualTreeAsset>(UxmlPath);
            if (asset == null)
            {
                GameLogger.Error("UI", $"UXML nicht gefunden: Resources/{UxmlPath}.uxml");
                _root = new VisualElement { name = $"missing-{Id}" };
                _root.Add(new Label($"[UXML fehlt: {UxmlPath}]"));
                return;
            }

            _root = asset.Instantiate();
            _root.name = $"screen-{Id}";
            _root.AddToClassList("ak-screen");
            _root.style.flexGrow = 1;

            BindElements(_root);
        }

        /// <summary>
        /// Wird einmalig nach UXML-Load aufgerufen. Hier per <see cref="Q{T}"/> Elements
        /// holen + Click-Handler / Bindings setzen. Subscribes auf Domain-Events kommen
        /// in <see cref="OnEnterAsync"/>, nicht hier.
        /// </summary>
        protected abstract void BindElements(VisualElement root);

        public virtual UniTask OnEnterAsync(CancellationToken ct) => UniTask.CompletedTask;
        public virtual UniTask OnLeaveAsync(CancellationToken ct) => UniTask.CompletedTask;

        /// <summary>
        /// Helper: Findet ein Element per Name. Wirft sichtbare Exception statt
        /// stiller NullRef wenn Name im UXML nicht existiert.
        /// </summary>
        protected T Q<T>(string name) where T : VisualElement
        {
            var el = Root.Q<T>(name);
            if (el == null)
                throw new System.InvalidOperationException(
                    $"Screen '{Id}': Element '{name}' (Typ {typeof(T).Name}) nicht im UXML gefunden.");
            return el;
        }

        /// <summary>Optional-Variante von <see cref="Q{T}"/>: gibt null zurueck statt zu werfen.</summary>
        protected T? QOptional<T>(string name) where T : VisualElement
            => Root.Q<T>(name);
    }
}
