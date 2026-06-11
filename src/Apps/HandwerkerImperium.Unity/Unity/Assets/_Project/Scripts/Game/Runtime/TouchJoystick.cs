using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Floating-Touch-Joystick (GDD §4: Android-Primärsteuerung): Finger setzt auf der linken
    /// Bildschirmhälfte den Ursprung, Ziehen liefert den normalisierten Bewegungs-Vektor
    /// (<see cref="Value"/>, vom <c>AvatarController</c> zusätzlich zu WASD/Gamepad gelesen).
    /// Visuelles Feedback (Ring + Knob) rendert ins HUD-<see cref="UIDocument"/> —
    /// PickingMode.Ignore, frisst also keine Button-Klicks. Ohne Touchscreen (Desktop-Test)
    /// simuliert die Maus auf der linken Hälfte.
    /// </summary>
    public sealed class TouchJoystick : MonoBehaviour
    {
        /// <summary>Aktive Instanz der Szene (vom AvatarController gepollt; null-tolerant).</summary>
        public static TouchJoystick Current { get; private set; }

        [Tooltip("HUD-Dokument für das Ring/Knob-Feedback (optional — ohne läuft der Stick unsichtbar).")]
        [SerializeField] private UIDocument hudDocument;
        [Tooltip("Voller Ausschlag in Screen-Pixeln.")]
        [SerializeField] private float radiusPx = 110f;
        [SerializeField] private float deadZone = 0.12f;

        private Vector2 _originScreen;
        private Vector2 _currentScreen;
        private bool _active;
        private Vector2 _value;
        private VisualElement _ring;
        private VisualElement _knob;

        /// <summary>Normalisierter Bewegungs-Vektor (Länge 0..1); Vector2.zero wenn inaktiv.</summary>
        public Vector2 Value => _active ? _value : Vector2.zero;

        private void Awake() => Current = this;

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        private void Start() => BuildVisuals();

        private void Update()
        {
            bool pressed = false;
            Vector2 pos = default;

            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                pressed = true;
                pos = touch.primaryTouch.position.ReadValue();
            }
            else if (touch == null)
            {
                // Desktop-Test ohne Touchscreen: Maus simuliert den Stick
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.isPressed)
                {
                    pressed = true;
                    pos = mouse.position.ReadValue();
                }
            }

            if (pressed && !_active && pos.x <= Screen.width * 0.5f)
            {
                _active = true;
                _originScreen = pos;
            }
            if (!pressed) _active = false;

            if (_active)
            {
                _currentScreen = pos;
                Vector2 v = Vector2.ClampMagnitude((pos - _originScreen) / Mathf.Max(1f, radiusPx), 1f);
                _value = v.magnitude < deadZone ? Vector2.zero : v;
            }

            UpdateVisuals();
        }

        private void BuildVisuals()
        {
            if (hudDocument == null) return;
            var root = hudDocument.rootVisualElement;
            if (root == null) return;

            _ring = MakeCircle(220f, new Color(1f, 1f, 1f, 0.06f), new Color(1f, 1f, 1f, 0.35f), 2f);
            _knob = MakeCircle(84f, new Color(1f, 1f, 1f, 0.28f), new Color(1f, 1f, 1f, 0.45f), 1.5f);
            root.Add(_ring);
            root.Add(_knob);
        }

        private static VisualElement MakeCircle(float size, Color fill, Color border, float borderWidth)
        {
            var el = new VisualElement();
            el.pickingMode = PickingMode.Ignore;
            el.style.position = Position.Absolute;
            el.style.width = size;
            el.style.height = size;
            el.style.backgroundColor = fill;
            el.style.borderTopLeftRadius = size * 0.5f;
            el.style.borderTopRightRadius = size * 0.5f;
            el.style.borderBottomLeftRadius = size * 0.5f;
            el.style.borderBottomRightRadius = size * 0.5f;
            el.style.borderLeftWidth = borderWidth;
            el.style.borderRightWidth = borderWidth;
            el.style.borderTopWidth = borderWidth;
            el.style.borderBottomWidth = borderWidth;
            el.style.borderLeftColor = border;
            el.style.borderRightColor = border;
            el.style.borderTopColor = border;
            el.style.borderBottomColor = border;
            el.style.display = DisplayStyle.None;
            return el;
        }

        private void UpdateVisuals()
        {
            if (_ring == null || _knob == null) return;
            if (!_active)
            {
                _ring.style.display = DisplayStyle.None;
                _knob.style.display = DisplayStyle.None;
                return;
            }

            var panel = hudDocument.rootVisualElement.panel;
            if (panel == null) return;
            Vector2 origin = ScreenToPanel(panel, _originScreen);
            Vector2 edge = ScreenToPanel(panel, _originScreen + new Vector2(radiusPx, 0f));
            float panelRadius = Mathf.Abs(edge.x - origin.x);
            Vector2 knobPos = origin + Vector2.ClampMagnitude(ScreenToPanel(panel, _currentScreen) - origin, panelRadius);

            Place(_ring, origin);
            Place(_knob, knobPos);
        }

        private static Vector2 ScreenToPanel(IPanel panel, Vector2 screenPos) =>
            RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y));

        private static void Place(VisualElement el, Vector2 panelCenter)
        {
            el.style.display = DisplayStyle.Flex;
            el.style.left = panelCenter.x - el.resolvedStyle.width * 0.5f;
            el.style.top = panelCenter.y - el.resolvedStyle.height * 0.5f;
        }
    }
}
