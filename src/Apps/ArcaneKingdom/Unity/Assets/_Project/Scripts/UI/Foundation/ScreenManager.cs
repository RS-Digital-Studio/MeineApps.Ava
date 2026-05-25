#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ArcaneKingdom.Core.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Foundation
{
    /// <summary>
    /// Zentraler UI-Router. Verwaltet einen Stack von Screens innerhalb eines
    /// UIDocument-Roots. Ein Screen ist entweder ein VOLL-Screen (Push verdrängt
    /// vorigen visuell) oder ein OVERLAY (legt sich darueber, voriger bleibt sichtbar).
    ///
    /// Verwendung:
    /// <code>
    ///   await _screenManager.PushAsync(ScreenId.Hub);
    ///   await _screenManager.PushAsync(ScreenId.DeckBuilder);
    ///   await _screenManager.PopAsync();          // Zurueck zum Hub
    ///   await _screenManager.ReplaceAsync(ScreenId.Battle); // Stack leeren -> Battle
    /// </code>
    ///
    /// Screens werden NICHT direkt instanziiert. Sie kommen aus dem DI-Container
    /// (VContainer) per <see cref="IScreenFactory"/>. So ist Constructor-Injection
    /// fuer Domain-Services moeglich.
    /// </summary>
    public sealed class ScreenManager : IDisposable
    {
        private readonly VisualElement _root;
        private readonly IScreenFactory _factory;
        private readonly Stack<IScreen> _stack = new();
        private readonly Dictionary<string, IScreen> _builtCache = new();
        private CancellationTokenSource _ctsActive = new();
        private bool _busy; // schuetzt vor Re-Entrance waehrend Transition

        public ScreenManager(VisualElement root, IScreenFactory factory)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public IReadOnlyCollection<IScreen> Stack => _stack;
        public IScreen? Current => _stack.Count > 0 ? _stack.Peek() : null;

        // ============================================================
        // Public Navigation API
        // ============================================================

        /// <summary>Legt einen Screen oben auf den Stack. Vorheriger wird versteckt
        /// (oder bleibt sichtbar wenn der neue ein Overlay ist).</summary>
        public async UniTask PushAsync(string screenId, CancellationToken ct = default)
        {
            if (!await TryAcquireBusyAsync(ct)) return;
            try
            {
                var screen = GetOrBuild(screenId);
                var previous = Current;
                _stack.Push(screen);

                AttachToRoot(screen);
                if (previous != null && !screen.IsOverlay)
                    SetVisible(previous, false);

                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _ctsActive.Token);
                await screen.OnEnterAsync(linked.Token);
                SetVisible(screen, true);

                GameLogger.Info("UI", $"Push '{screenId}' (Stack: {_stack.Count})");
            }
            finally { _busy = false; }
        }

        /// <summary>Entfernt den obersten Screen. Vorheriger wird wieder sichtbar.</summary>
        public async UniTask PopAsync(CancellationToken ct = default)
        {
            if (_stack.Count <= 1)
            {
                GameLogger.Warning("UI", "Pop ignoriert: nur 1 Screen oder leer.");
                return;
            }
            if (!await TryAcquireBusyAsync(ct)) return;
            try
            {
                var screen = _stack.Pop();
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _ctsActive.Token);
                await screen.OnLeaveAsync(linked.Token);

                SetVisible(screen, false);
                DetachFromRoot(screen);

                var next = Current;
                if (next != null && !screen.IsOverlay)
                    SetVisible(next, true);

                GameLogger.Info("UI", $"Pop '{screen.Id}' (Stack: {_stack.Count})");
            }
            finally { _busy = false; }
        }

        /// <summary>Leert den Stack komplett und zeigt nur <paramref name="screenId"/>.
        /// Verwendet z.B. fuer Login -&gt; Hub oder Hub -&gt; Battle.</summary>
        public async UniTask ReplaceAsync(string screenId, CancellationToken ct = default)
        {
            if (!await TryAcquireBusyAsync(ct)) return;
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _ctsActive.Token);

                // Alle aktuellen Screens leaven
                while (_stack.Count > 0)
                {
                    var s = _stack.Pop();
                    await s.OnLeaveAsync(linked.Token);
                    SetVisible(s, false);
                    DetachFromRoot(s);
                }

                var screen = GetOrBuild(screenId);
                _stack.Push(screen);
                AttachToRoot(screen);
                await screen.OnEnterAsync(linked.Token);
                SetVisible(screen, true);

                GameLogger.Info("UI", $"Replace -> '{screenId}'");
            }
            finally { _busy = false; }
        }

        /// <summary>Pop bis ein bestimmter Screen oben liegt (z.B. zurueck zum Hub).</summary>
        public async UniTask PopToAsync(string screenId, CancellationToken ct = default)
        {
            while (_stack.Count > 1 && Current?.Id != screenId)
                await PopAsync(ct);
        }

        // ============================================================
        // Internals
        // ============================================================

        private IScreen GetOrBuild(string id)
        {
            if (_builtCache.TryGetValue(id, out var cached))
                return cached;

            var screen = _factory.Create(id);
            screen.Build();
            _builtCache[id] = screen;
            return screen;
        }

        private void AttachToRoot(IScreen screen)
        {
            if (screen.Root.parent == null)
                _root.Add(screen.Root);
        }

        private void DetachFromRoot(IScreen screen)
        {
            if (screen.Root.parent != null)
                screen.Root.RemoveFromHierarchy();
        }

        private static void SetVisible(IScreen screen, bool visible)
        {
            screen.Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible)
                screen.Root.RemoveFromClassList("ak-screen--hidden");
            else
                screen.Root.AddToClassList("ak-screen--hidden");
        }

        private async UniTask<bool> TryAcquireBusyAsync(CancellationToken ct)
        {
            // Sehr einfache Re-Entrance-Bremse: laufende Navigation darf nicht
            // unterbrochen werden. Wer waehrend Transition pusht/popt, wartet ab.
            var waited = 0;
            while (_busy)
            {
                if (waited > 50)
                {
                    GameLogger.Warning("UI", "Navigation-Timeout (>500ms busy).");
                    return false;
                }
                if (ct.IsCancellationRequested) return false;
                await UniTask.Delay(10, cancellationToken: ct);
                waited++;
            }
            _busy = true;
            return true;
        }

        public void Dispose()
        {
            _ctsActive.Cancel();
            _ctsActive.Dispose();
            _builtCache.Clear();
            _stack.Clear();
        }
    }
}
