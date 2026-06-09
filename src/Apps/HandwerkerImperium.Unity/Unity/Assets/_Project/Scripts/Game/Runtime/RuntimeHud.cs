using UnityEngine;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Diagnose-/Dev-HUD (IMGUI, asset-frei) über dem <see cref="RuntimeGameController"/>: zeigt den Live-Zustand
    /// (Geld, effektives Einkommen/s, Stern, Prestige, Meisterschaft, Kunden, Offline-Verdienst) und bietet
    /// Test-Buttons (Prestige/Speichern). So ist das verdrahtete Runtime <b>sofort im Play-Mode prüfbar</b> — ohne
    /// 3D-Assets. Die finale Premium-UI (UI Toolkit) ersetzt dieses HUD in der Präsentations-Phase.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuntimeHud : MonoBehaviour
    {
        [SerializeField] private RuntimeGameController controller;

        private void OnGUI()
        {
            if (controller == null || controller.Model == null) return;
            var m = controller.Model;

            GUILayout.BeginArea(new Rect(12, 12, 380, 460), GUI.skin.box);
            GUILayout.Label("<b>HandwerkerImperium — Runtime (Diagnose-HUD)</b>");
            GUILayout.Space(4);
            GUILayout.Label($"Geld:        {m.Idle.Money:N0}");
            GUILayout.Label($"Gems:        {m.Gems:N0}");
            GUILayout.Label($"Einkommen/s: {controller.EffectiveIncomePerSecond():N2} (effektiv)");
            GUILayout.Label($"Stern:       {controller.EvaluateStar()}★   Stadt: {m.Meta.CityIndex}");
            GUILayout.Label($"Prestige:    {m.Meta.PrestigeCount}  (x{m.Meta.PrestigeMultiplier})  Marken: {m.Meta.AvailableMarks}");
            GUILayout.Label($"Meisterschaft: Lv {m.Meta.MasteryLevel}   Meistergrad: {m.Meta.MeistergradGrade}");
            GUILayout.Label($"Kunden:      {m.Orders.PendingCustomers} warten · {m.Orders.TotalServed} bedient");
            if (controller.LastOfflineEarned > 0m)
                GUILayout.Label($"Offline-Verdienst: {controller.LastOfflineEarned:N0}");

            GUILayout.Space(8);
            GUILayout.Label("<i>Test-Aktionen</i>");
            if (GUILayout.Button("+1.000 Geld")) m.Idle.Money += 1000m;
            if (GUILayout.Button("Worker an Station 0 anstellen") && m.Idle.Stations.Count > 0)
                m.Idle.Stations[0].HasWorker = true;
            if (GUILayout.Button("5 Sterne setzen (Prestige freischalten)")) m.Meta.CurrentStar = 5;

            GUI.enabled = controller.CanPrestige();
            if (GUILayout.Button("PRESTIGE — Umzug in die nächste Stadt")) controller.TryPrestige();
            GUI.enabled = true;

            if (GUILayout.Button("Speichern")) controller.PersistNow();
            GUILayout.EndArea();
        }
    }
}
