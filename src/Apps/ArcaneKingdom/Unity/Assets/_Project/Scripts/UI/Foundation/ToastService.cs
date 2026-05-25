#nullable enable
using System.Collections.Generic;
using ArcaneKingdom.Core.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Foundation
{
    /// <summary>Visueller Style einer Toast-Nachricht.</summary>
    public enum ToastKind { Info, Success, Warning, Danger }

    /// <summary>
    /// Zeigt kurze Nachrichten oben im OverlayContainer. Mehrere Toasts werden untereinander gestapelt
    /// und nach Ablauf automatisch ausgeblendet.
    /// </summary>
    public sealed class ToastService
    {
        private readonly UIRoot _ui;
        private readonly List<VisualElement> _activeToasts = new();
        private const float DefaultDurationSeconds = 3f;

        public ToastService(UIRoot ui) => _ui = ui;

        public void Show(string message, ToastKind kind = ToastKind.Info, float? durationSeconds = null)
        {
            if (_ui.OverlayContainer == null)
            {
                GameLogger.Warning("Toast", $"OverlayContainer null — Toast verworfen: {message}");
                return;
            }

            var toast = new VisualElement { name = "toast" };
            toast.AddToClassList("ak-toast");
            toast.AddToClassList(kind switch
            {
                ToastKind.Success => "ak-toast--success",
                ToastKind.Warning => "ak-toast--warning",
                ToastKind.Danger => "ak-toast--danger",
                _ => "ak-toast--info"
            });

            var label = new Label(message);
            label.AddToClassList("ak-toast__text");
            toast.Add(label);

            // Stack-Position berechnen (jeder Toast 12px unter dem vorherigen)
            var topOffset = 80f + _activeToasts.Count * 60f;
            toast.style.top = topOffset;

            _ui.OverlayContainer.Add(toast);
            _activeToasts.Add(toast);

            AutoHideAsync(toast, durationSeconds ?? DefaultDurationSeconds).Forget();
        }

        private async UniTaskVoid AutoHideAsync(VisualElement toast, float seconds)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(seconds));
            // Fade-Out
            toast.style.opacity = 0;
            await UniTask.Delay(System.TimeSpan.FromMilliseconds(220));

            toast.RemoveFromHierarchy();
            _activeToasts.Remove(toast);

            // Verbleibende Toasts nachruecken
            for (int i = 0; i < _activeToasts.Count; i++)
                _activeToasts[i].style.top = 80f + i * 60f;
        }
    }
}
