#nullable enable
using System.Collections.Generic;
using System.Linq;
using ArcaneKingdom.Domain.Cards;
using UnityEditor;
using UnityEngine;

namespace ArcaneKingdom.EditorTools.Inspectors
{
    /// <summary>
    /// Balancing-Dashboard: Stat-Verteilung aller Karten visualisiert.
    /// Histogramme für ATK/HP/Cost/Turns + Verteilung pro Element/Rasse/Rarity.
    /// </summary>
    public sealed class BalancingDashboard : EditorWindow
    {
        private List<CardDefinition> _cards = new();
        private Vector2 _scroll;

        [MenuItem("ArcaneKingdom/Inspectors/Balancing Dashboard")]
        private static void Open() => GetWindow<BalancingDashboard>("Balancing").Refresh();

        public void Refresh()
        {
            _cards = AssetDatabase.FindAssets("t:CardDefinition")
                .Select(g => AssetDatabase.LoadAssetAtPath<CardDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null)
                .Select(c => c!)
                .ToList();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70))) Refresh();
            GUILayout.Label($"Karten: {_cards.Count}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            Section("Rarity-Verteilung", _cards.GroupBy(c => c.Rarity.ToString()).ToDictionary(g => g.Key, g => g.Count()));
            Section("Element-Verteilung", _cards.GroupBy(c => c.Element.ToString()).ToDictionary(g => g.Key, g => g.Count()));
            Section("Rasse-Verteilung", _cards.GroupBy(c => c.Race.ToString()).ToDictionary(g => g.Key, g => g.Count()));
            Section("Cost-Verteilung", _cards.GroupBy(c => c.Cost.ToString()).OrderBy(g => int.Parse(g.Key)).ToDictionary(g => $"Cost {g.Key}", g => g.Count()));

            EditorGUILayout.LabelField("Stat-Mittelwerte pro Rarity:", EditorStyles.boldLabel);
            foreach (var grp in _cards.GroupBy(c => c.Rarity).OrderBy(g => g.Key))
            {
                var atk = grp.Average(c => c.BaseAttack);
                var hp = grp.Average(c => c.BaseHealth);
                var cost = grp.Average(c => c.Cost);
                var stat = grp.Average(c => (c.BaseAttack + c.BaseHealth) / (float)c.Cost);
                EditorGUILayout.LabelField($"{grp.Key,-15} N={grp.Count(),-3}  ATK avg {atk:F0}  HP avg {hp:F0}  Cost avg {cost:F1}  Stat/Cost {stat:F0}");
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Power-Outlier (Stat/Cost):", EditorStyles.boldLabel);
            var sorted = _cards.OrderByDescending(c => (c.BaseAttack + c.BaseHealth) / (float)c.Cost).Take(8);
            foreach (var c in sorted)
                EditorGUILayout.LabelField($"  {c.Id,-30} Stat/Cost {(c.BaseAttack + c.BaseHealth) / (float)c.Cost,6:F0}  [Cost {c.Cost} | ATK {c.BaseAttack} | HP {c.BaseHealth}]");

            EditorGUILayout.EndScrollView();
        }

        private static void Section(string title, Dictionary<string, int> data)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            var maxVal = data.Count == 0 ? 1 : data.Values.Max();
            foreach (var kv in data)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(kv.Key, GUILayout.Width(140));
                var rect = GUILayoutUtility.GetRect(200, 14);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width * ((float)kv.Value / maxVal), rect.height), new Color(0.30f, 0.55f, 0.95f));
                GUILayout.Label($"  {kv.Value}", GUILayout.Width(40));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.Space(6);
        }
    }
}
