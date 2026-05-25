#nullable enable
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Common
{
    /// <summary>
    /// Manipulator der ein Karten-Tile per Drag&amp;Drop verschiebbar macht.
    ///
    /// Verwendung:
    /// <code>
    ///   var drag = new CardDragManipulator(
    ///       dropZone: _playerField,
    ///       onDrop: () => OnPlayCard(def),
    ///       canDrag: () => _state.PlayerMana >= def.Cost,
    ///       floatingLayer: _floatingLayer);
    ///   cardTile.AddManipulator(drag);
    /// </code>
    ///
    /// Verhalten:
    ///   - PointerDown: prüft <see cref="_canDrag"/>; wenn ok wird ein Ghost-Clone
    ///     im Floating-Layer angelegt
    ///   - PointerMove: Ghost folgt dem Pointer
    ///   - PointerUp: wenn Pointer über DropZone -&gt; <see cref="_onDrop"/>
    ///                sonst Ghost faded weg, Original bleibt unverändert
    /// </summary>
    public sealed class CardDragManipulator : IManipulator
    {
        private readonly VisualElement _dropZone;
        private readonly Action _onDrop;
        private readonly Func<bool>? _canDrag;
        private readonly VisualElement _floatingLayer;

        private VisualElement? _target;
        private VisualElement? _ghost;
        private bool _dragging;
        private int _pointerId = -1;

        public VisualElement? target
        {
            get => _target;
            set
            {
                if (_target != null)
                {
                    _target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
                    _target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
                    _target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
                    _target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
                }
                _target = value;
                if (_target != null)
                {
                    _target.RegisterCallback<PointerDownEvent>(OnPointerDown);
                    _target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                    _target.RegisterCallback<PointerUpEvent>(OnPointerUp);
                    _target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
                }
            }
        }

        public CardDragManipulator(VisualElement dropZone,
                                   Action onDrop,
                                   VisualElement floatingLayer,
                                   Func<bool>? canDrag = null)
        {
            _dropZone = dropZone;
            _onDrop = onDrop;
            _floatingLayer = floatingLayer;
            _canDrag = canDrag;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (_target == null) return;
            if (_canDrag != null && !_canDrag.Invoke()) return;
            if (evt.button != 0) return;

            _dragging = true;
            _pointerId = evt.pointerId;
            _target.CapturePointer(_pointerId);

            _ghost = CreateGhost(_target);
            _floatingLayer.Add(_ghost);
            PositionGhost(evt.position);

            _target.style.opacity = 0.35f;
            _dropZone.AddToClassList("ak-drop-zone");
            _dropZone.AddToClassList("ak-drop-zone--active");
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_dragging || _ghost == null) return;
            PositionGhost(evt.position);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_dragging || _target == null || _ghost == null) return;
            EndDrag(evt.position, releaseCapture: true);
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            // Sicherheits-Cleanup wenn Capture verloren wird (z.B. App-Focus)
            if (_dragging) EndDrag(Vector2.zero, releaseCapture: false, treatAsCancel: true);
        }

        private void EndDrag(Vector2 worldPosition, bool releaseCapture, bool treatAsCancel = false)
        {
            if (_target == null) return;

            if (releaseCapture)
                _target.ReleasePointer(_pointerId);

            _target.style.opacity = 1f;

            // Drop-Test: liegt Pointer im DropZone-Rect?
            var dropped = false;
            if (!treatAsCancel && _dropZone.worldBound.Contains(worldPosition))
            {
                dropped = true;
            }

            if (_ghost != null)
            {
                _ghost.RemoveFromHierarchy();
                _ghost = null;
            }

            _dropZone.RemoveFromClassList("ak-drop-zone--active");

            _dragging = false;
            _pointerId = -1;

            if (dropped) _onDrop.Invoke();
        }

        private VisualElement CreateGhost(VisualElement source)
        {
            var ghost = new VisualElement();
            ghost.AddToClassList("ak-card");
            // Style auf Source-Größe spiegeln
            ghost.style.width = source.resolvedStyle.width;
            ghost.style.height = source.resolvedStyle.height;
            ghost.style.position = Position.Absolute;
            ghost.style.opacity = 0.85f;
            ghost.style.scale = new StyleScale(new Scale(new Vector2(1.05f, 1.05f)));
            ghost.pickingMode = PickingMode.Ignore;

            // Optional: Source-Klassen kopieren für Rarity-Border
            foreach (var cls in source.GetClasses())
                if (cls.StartsWith("ak-card--rarity-"))
                    ghost.AddToClassList(cls);

            // Label "Drop hier!" als visuelles Feedback
            var hint = new Label("Drop");
            hint.style.fontSize = 12;
            hint.style.color = new StyleColor(new Color(0.95f, 0.78f, 0.30f));
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.position = Position.Absolute;
            hint.style.bottom = 4;
            hint.style.left = 0;
            hint.style.right = 0;
            ghost.Add(hint);
            return ghost;
        }

        private void PositionGhost(Vector2 worldPosition)
        {
            if (_ghost == null) return;
            var local = _floatingLayer.WorldToLocal(worldPosition);
            var halfW = _ghost.resolvedStyle.width * 0.5f;
            var halfH = _ghost.resolvedStyle.height * 0.5f;
            _ghost.style.left = local.x - halfW;
            _ghost.style.top = local.y - halfH;
        }
    }
}
