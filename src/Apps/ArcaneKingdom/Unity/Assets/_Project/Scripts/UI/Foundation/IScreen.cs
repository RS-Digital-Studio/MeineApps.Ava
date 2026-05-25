#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Foundation
{
    /// <summary>
    /// Vertrag für alle Screens (Login, Hub, Battle, Modals, ...).
    /// Ein Screen liefert ein <see cref="VisualElement"/>-Root, das vom
    /// <see cref="ScreenManager"/> in den UIDocument-Tree gehangen wird.
    ///
    /// Lifecycle:
    ///   1. <see cref="Build"/>             — UXML laden + Bindings setzen (einmalig)
    ///   2. <see cref="OnEnterAsync"/>      — vor dem Anzeigen (Daten laden)
    ///   3. <see cref="Root"/> wird sichtbar gemacht
    ///   4. <see cref="OnLeaveAsync"/>      — vor dem Verstecken (Cleanup, Save)
    ///   5. ggf. <see cref="Dispose"/>      — wenn Screen gestoppt wird
    /// </summary>
    public interface IScreen
    {
        /// <summary>Die ID dieses Screens (siehe <see cref="ScreenId"/>).</summary>
        string Id { get; }

        /// <summary>
        /// Der gebaute Root. Wird vom ScreenManager in den UIDocument-Tree gehangen.
        /// Darf erst nach <see cref="Build"/> abgerufen werden.
        /// </summary>
        VisualElement Root { get; }

        /// <summary>
        /// Wenn true, bleibt der Screen DARUNTER stehen (z.B. Overlays über dem Hub).
        /// Wenn false, wird der vorherige Screen versteckt (z.B. Hub -> Battle).
        /// </summary>
        bool IsOverlay { get; }

        /// <summary>
        /// Einmalige Konstruktion: UXML laden, USS verlinken, Bindings setzen.
        /// Wird beim ersten Show des Screens vom ScreenManager aufgerufen.
        /// </summary>
        void Build();

        /// <summary>Wird vor dem Anzeigen gerufen. Hier Daten laden / Subscribes.</summary>
        UniTask OnEnterAsync(CancellationToken ct);

        /// <summary>Wird vor dem Verstecken gerufen. Hier Unsubscribes / Save.</summary>
        UniTask OnLeaveAsync(CancellationToken ct);
    }
}
