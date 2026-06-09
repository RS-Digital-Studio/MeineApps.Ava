using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Diagnose-/Dev-HUD (IMGUI, asset-frei) über dem <see cref="RuntimeGameController"/>: zeigt den Live-Zustand
    /// (Geld, effektives Einkommen/s, Stern, Prestige, Meisterschaft, Kunden, Offline-Verdienst) + Test-Buttons.
    /// Als <b>verschiebbares, selbst-höhen-anpassendes Fenster</b> (GUILayout.Window) — passt sich automatisch an den
    /// Inhalt an (nichts wird abgeschnitten) und ist per Titelleiste an jede Stelle ziehbar (robust gegen Game-View-Zoom).
    /// Schrift nach Monitorhöhe skaliert. Die finale Premium-UI (UI Toolkit) ersetzt es in der Präsentations-Phase.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuntimeHud : MonoBehaviour
    {
        [SerializeField] private RuntimeGameController controller;
        [Tooltip("Referenz-Bildschirmhöhe für die Schrift-Skalierung (kleiner = größeres HUD).")]
        [SerializeField] private float referenceHeight = 820f;

        private Rect _win = new Rect(20f, 20f, 460f, 120f);
        private float _k = -1f;
        private GUIStyle _window, _label, _title, _button;

        private void BuildStyles(float k)
        {
            int P(float v) => Mathf.RoundToInt(v * k);
            _window = new GUIStyle(GUI.skin.window) { fontSize = P(15), padding = new RectOffset(P(14), P(14), P(24), P(14)) };
            _label = new GUIStyle(GUI.skin.label) { fontSize = P(16), richText = true, wordWrap = false };
            _title = new GUIStyle(GUI.skin.label) { fontSize = P(19), richText = true, fontStyle = FontStyle.Bold };
            _button = new GUIStyle(GUI.skin.button) { fontSize = P(15), padding = new RectOffset(P(10), P(10), P(8), P(8)) };
        }

        private void OnGUI()
        {
            if (controller == null || controller.Model == null) return;

            float k = Mathf.Clamp(Screen.height / referenceHeight, 1f, 3f);
            if (_window == null || !Mathf.Approximately(k, _k)) { _k = k; BuildStyles(k); }

            _win.width = Mathf.Min(560f * k, Screen.width - 20f);
            _win = GUILayout.Window(GetInstanceID(), _win, DrawWindow, "Runtime — Diagnose (Titelleiste zum Ziehen)", _window);
        }

        private void DrawWindow(int id)
        {
            var m = controller.Model;
            GUILayout.Label("<b>HandwerkerImperium — Runtime</b>", _title);
            GUILayout.Space(6f * _k);
            GUILayout.Label($"Geld:           <b>{m.Idle.Money:N0}</b>", _label);
            GUILayout.Label($"Gems:           {m.Gems:N0}", _label);
            GUILayout.Label($"Einkommen/s:    <b>{controller.EffectiveIncomePerSecond():N2}</b>  (effektiv)", _label);
            GUILayout.Label($"Stern:          {controller.EvaluateStar()}★   Stadt: {m.Meta.CityIndex}", _label);
            GUILayout.Label($"Prestige:       {m.Meta.PrestigeCount}  (x{m.Meta.PrestigeMultiplier})   Marken: {m.Meta.AvailableMarks}", _label);
            GUILayout.Label($"Meisterschaft:  Lv {m.Meta.MasteryLevel}    Meistergrad: {m.Meta.MeistergradGrade}", _label);
            GUILayout.Label($"Kunden:         {m.Orders.PendingCustomers} warten · {m.Orders.TotalServed} bedient", _label);
            if (controller.LastOfflineEarned > 0m)
                GUILayout.Label($"Offline-Verdienst: <b>{controller.LastOfflineEarned:N0}</b>", _label);

            GUILayout.Space(10f * _k);
            if (GUILayout.Button("+1.000 Geld", _button)) m.Idle.Money += 1000m;
            if (GUILayout.Button("Worker an Station 0 anstellen", _button) && m.Idle.Stations.Count > 0)
                m.Idle.Stations[0].HasWorker = true;
            if (GUILayout.Button("5 Sterne setzen (Prestige freischalten)", _button)) m.Meta.CurrentStar = 5;

            GUI.enabled = controller.CanPrestige();
            if (GUILayout.Button("PRESTIGE — Umzug in die nächste Stadt", _button)) controller.TryPrestige();
            GUI.enabled = true;

            if (GUILayout.Button("Speichern", _button)) controller.PersistNow();

            GUI.DragWindow(); // per Titelleiste/Hintergrund verschiebbar
        }
    }
}
