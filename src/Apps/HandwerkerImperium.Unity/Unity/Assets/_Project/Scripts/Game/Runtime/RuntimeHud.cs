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
            bool hasStation = m.Idle.Stations.Count > 0;

            GUILayout.Label("<b>HandwerkerImperium — Runtime</b>", _title);
            GUILayout.Space(6f * _k);
            GUILayout.Label($"Geld:           <b>{m.Idle.Money:N0}</b>", _label);
            GUILayout.Label($"Gems:           {m.Gems:N0}", _label);
            GUILayout.Label($"Einkommen/s:    <b>{controller.EffectiveIncomePerSecond():N2}</b>  (effektiv)", _label);
            // Stern nur LESEN (EvaluateStar mutiert -> nicht pro Frame aufrufen, sonst ueberschreibt es den Test-Wert)
            GUILayout.Label($"Stern:          {m.Meta.CurrentStar}★   Stadt: {m.Meta.CityIndex}", _label);
            GUILayout.Label($"Prestige:       {m.Meta.PrestigeCount}  (x{m.Meta.PrestigeMultiplier})   Marken: {m.Meta.AvailableMarks}", _label);
            GUILayout.Label($"Meisterschaft:  Lv {m.Meta.MasteryLevel}    Meistergrad: {m.Meta.MeistergradGrade}", _label);
            GUILayout.Label($"Master-Tools:   {controller.CollectedToolsCount}/12    Achievements: {controller.AchievementsCount}", _label);
            GUILayout.Label($"Kunden:         {m.Orders.PendingCustomers} warten · {m.Orders.TotalServed} bedient", _label);
            GUILayout.Label($"Saison: {controller.CurrentSeason()}    Rush: <b>{(controller.RushActive() ? "AKTIV (2×)" : "inaktiv")}</b>", _label);
            if (!string.IsNullOrEmpty(controller.LatestStoryBeat))
                GUILayout.Label($"Hans (Beat):    <i>{controller.LatestStoryBeat}</i>", _label);
            if (m.DailyTasks != null && m.DailyTasks.Count > 0)
            {
                GUILayout.Label("<b>Tagesaufgaben:</b>", _label);
                foreach (var t in m.DailyTasks)
                {
                    int pct = (int)(controller.DailyTaskProgress01(t) * 100.0);
                    string state = t.Claimed ? "<b>fertig (+" + t.GemReward + " Gems)</b>" : pct + "%";
                    GUILayout.Label($"  · {t.Id}: {state}", _label);
                }
            }
            if (hasStation)
            {
                var s0 = m.Idle.Stations[0];
                GUILayout.Label($"Station 0:      Stock {s0.Stock}   Worker: <b>{(s0.HasWorker ? "JA" : "nein")}</b>", _label);
            }
            if (controller.LastOfflineEarned > 0m)
                GUILayout.Label($"Offline-Verdienst: <b>{controller.LastOfflineEarned:N0}</b>", _label);

            GUILayout.Space(10f * _k);
            if (GUILayout.Button("+1.000 Geld", _button)) m.Idle.Money += 1000m;
            if (hasStation && GUILayout.Button(m.Idle.Stations[0].HasWorker ? "Worker Station 0 ENTLASSEN" : "Worker an Station 0 anstellen", _button))
                m.Idle.Stations[0].HasWorker = !m.Idle.Stations[0].HasWorker;
            if (GUILayout.Button("Kunde bedienen (+Geld, wenn Kunde wartet)", _button)) controller.ServeCustomer(0);
            if (GUILayout.Button("Tempo-Upgrade kaufen (kostet Geld)", _button)) controller.BuyTempoUpgrade();
            if (GUILayout.Button("+250 Meisterschafts-XP", _button)) controller.GainMastery(250.0);
            if (GUILayout.Button("Tagesbelohnung abholen", _button)) controller.ClaimDaily();
            if (GUILayout.Button("Free-Cash abholen (2× Einkommen, 'Ad')", _button)) controller.ClaimFreeCash();
            if (GUILayout.Button("Rush-Event starten (alle Stationen 2×)", _button)) controller.StartRush();
            if (GUILayout.Button("Perk Global-Tempo kaufen (Marken)", _button)) controller.BuyTempoPerk();
            if (GUILayout.Button("Meistergrad kaufen (Endstadt, Renommee)", _button)) controller.BuyMeistergrad();
            if (GUILayout.Button("5 Sterne setzen (Prestige freischalten)", _button)) m.Meta.CurrentStar = 5;
            if (GUILayout.Button("Stern aus Fortschritt neu bewerten", _button)) controller.EvaluateStar();

            GUI.enabled = controller.CanPrestige();
            if (GUILayout.Button("PRESTIGE — Umzug in die nächste Stadt", _button)) controller.TryPrestige();
            GUI.enabled = true;

            if (GUILayout.Button("Speichern", _button)) controller.PersistNow();

            GUI.DragWindow(); // per Titelleiste/Hintergrund verschiebbar
        }
    }
}
