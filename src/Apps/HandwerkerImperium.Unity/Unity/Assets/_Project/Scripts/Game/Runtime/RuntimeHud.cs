using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Diagnose-/Dev-HUD (IMGUI, asset-frei) über dem <see cref="RuntimeGameController"/>: zeigt den Live-Zustand
    /// (Geld, effektives Einkommen/s, Stern, Prestige, Meisterschaft, Kunden, Offline-Verdienst) + Test-Buttons.
    /// <b>Direkt skalierte Schriftgrößen</b> (kein GUI.matrix — das clippt BeginArea falsch) und ein Panel über die
    /// volle Bildschirmhöhe, damit auf hochauflösenden Monitoren alles lesbar bleibt und nichts abgeschnitten wird.
    /// Macht das verdrahtete Runtime sofort im Play-Mode prüfbar — die finale Premium-UI (UI Toolkit) ersetzt es später.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuntimeHud : MonoBehaviour
    {
        [SerializeField] private RuntimeGameController controller;
        [Tooltip("Referenz-Bildschirmhöhe für die Schrift-Skalierung (kleiner = größeres HUD).")]
        [SerializeField] private float referenceHeight = 820f;

        private float _k = -1f;
        private GUIStyle _box, _label, _title, _button;

        private void BuildStyles(float k)
        {
            int Pad(float v) => Mathf.RoundToInt(v * k);
            _box = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(Pad(16), Pad(16), Pad(14), Pad(14)),
                alignment = TextAnchor.UpperLeft
            };
            _label = new GUIStyle(GUI.skin.label) { fontSize = Pad(16), richText = true, wordWrap = false };
            _title = new GUIStyle(GUI.skin.label) { fontSize = Pad(20), richText = true, fontStyle = FontStyle.Bold };
            _button = new GUIStyle(GUI.skin.button) { fontSize = Pad(15), padding = new RectOffset(Pad(10), Pad(10), Pad(8), Pad(8)) };
        }

        private void OnGUI()
        {
            if (controller == null || controller.Model == null) return;

            float k = Mathf.Clamp(Screen.height / referenceHeight, 1f, 3f);
            if (_box == null || !Mathf.Approximately(k, _k)) { _k = k; BuildStyles(k); }

            var m = controller.Model;
            float w = Mathf.Min(540f * k, Screen.width - 20f);
            float h = Screen.height - 20f; // volle Höhe -> nichts wird abgeschnitten

            GUILayout.BeginArea(new Rect(10f, 10f, w, h), _box);
            GUILayout.Label("HandwerkerImperium — Runtime (Diagnose-HUD)", _title);
            GUILayout.Space(8f * k);
            GUILayout.Label($"Geld:           <b>{m.Idle.Money:N0}</b>", _label);
            GUILayout.Label($"Gems:           {m.Gems:N0}", _label);
            GUILayout.Label($"Einkommen/s:    <b>{controller.EffectiveIncomePerSecond():N2}</b>  (effektiv)", _label);
            GUILayout.Label($"Stern:          {controller.EvaluateStar()}★   Stadt: {m.Meta.CityIndex}", _label);
            GUILayout.Label($"Prestige:       {m.Meta.PrestigeCount}  (x{m.Meta.PrestigeMultiplier})   Marken: {m.Meta.AvailableMarks}", _label);
            GUILayout.Label($"Meisterschaft:  Lv {m.Meta.MasteryLevel}    Meistergrad: {m.Meta.MeistergradGrade}", _label);
            GUILayout.Label($"Kunden:         {m.Orders.PendingCustomers} warten · {m.Orders.TotalServed} bedient", _label);
            if (controller.LastOfflineEarned > 0m)
                GUILayout.Label($"Offline-Verdienst: <b>{controller.LastOfflineEarned:N0}</b>", _label);

            GUILayout.Space(10f * k);
            GUILayout.Label("<i>Test-Aktionen</i>", _label);
            if (GUILayout.Button("+1.000 Geld", _button)) m.Idle.Money += 1000m;
            if (GUILayout.Button("Worker an Station 0 anstellen", _button) && m.Idle.Stations.Count > 0)
                m.Idle.Stations[0].HasWorker = true;
            if (GUILayout.Button("5 Sterne setzen (Prestige freischalten)", _button)) m.Meta.CurrentStar = 5;

            GUI.enabled = controller.CanPrestige();
            if (GUILayout.Button("PRESTIGE — Umzug in die nächste Stadt", _button)) controller.TryPrestige();
            GUI.enabled = true;

            if (GUILayout.Button("Speichern", _button)) controller.PersistNow();
            GUILayout.EndArea();
        }
    }
}
