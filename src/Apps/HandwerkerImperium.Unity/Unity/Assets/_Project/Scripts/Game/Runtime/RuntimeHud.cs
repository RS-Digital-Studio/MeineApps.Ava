using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Diagnose-/Dev-HUD (IMGUI, asset-frei) über dem <see cref="RuntimeGameController"/>: zeigt den Live-Zustand
    /// (Geld, effektives Einkommen/s, Stern, Prestige, Meisterschaft, Kunden, Offline-Verdienst) und bietet
    /// Test-Buttons (Prestige/Speichern). DPI-skaliert + klar oben-links verankert, damit es auf hochauflösenden
    /// Monitoren lesbar bleibt. So ist das verdrahtete Runtime <b>sofort im Play-Mode prüfbar</b> — ohne 3D-Assets.
    /// Die finale Premium-UI (UI Toolkit) ersetzt dieses HUD in der Präsentations-Phase.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuntimeHud : MonoBehaviour
    {
        [SerializeField] private RuntimeGameController controller;
        [Tooltip("Referenz-Bildschirmhöhe für die DPI-Skalierung des HUDs.")]
        [SerializeField] private float referenceHeight = 820f;

        private GUIStyle _box;
        private GUIStyle _label;
        private GUIStyle _title;
        private GUIStyle _button;

        private void EnsureStyles()
        {
            if (_box != null) return;
            _box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(16, 16, 14, 14), alignment = TextAnchor.UpperLeft };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true, wordWrap = false };
            _title = new GUIStyle(GUI.skin.label) { fontSize = 19, richText = true, fontStyle = FontStyle.Bold };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 15, padding = new RectOffset(10, 10, 7, 7) };
        }

        private void OnGUI()
        {
            if (controller == null || controller.Model == null) return;
            EnsureStyles();
            var m = controller.Model;

            // DPI-/Auflösungs-Skalierung: bei hoher Bildschirmhöhe wird das HUD entsprechend größer gezeichnet.
            float scale = Mathf.Max(1f, Screen.height / referenceHeight);
            Matrix4x4 prev = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(24f, 24f, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));

            GUILayout.BeginArea(new Rect(0, 0, 470, 540), _box);
            GUILayout.Label("HandwerkerImperium — Runtime (Diagnose-HUD)", _title);
            GUILayout.Space(8);
            GUILayout.Label($"Geld:           <b>{m.Idle.Money:N0}</b>", _label);
            GUILayout.Label($"Gems:           {m.Gems:N0}", _label);
            GUILayout.Label($"Einkommen/s:    <b>{controller.EffectiveIncomePerSecond():N2}</b>  (effektiv)", _label);
            GUILayout.Label($"Stern:          {controller.EvaluateStar()}★   Stadt: {m.Meta.CityIndex}", _label);
            GUILayout.Label($"Prestige:       {m.Meta.PrestigeCount}  (x{m.Meta.PrestigeMultiplier})   Marken: {m.Meta.AvailableMarks}", _label);
            GUILayout.Label($"Meisterschaft:  Lv {m.Meta.MasteryLevel}    Meistergrad: {m.Meta.MeistergradGrade}", _label);
            GUILayout.Label($"Kunden:         {m.Orders.PendingCustomers} warten · {m.Orders.TotalServed} bedient", _label);
            if (controller.LastOfflineEarned > 0m)
                GUILayout.Label($"Offline-Verdienst: <b>{controller.LastOfflineEarned:N0}</b>", _label);

            GUILayout.Space(10);
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

            GUI.matrix = prev;
        }
    }
}
