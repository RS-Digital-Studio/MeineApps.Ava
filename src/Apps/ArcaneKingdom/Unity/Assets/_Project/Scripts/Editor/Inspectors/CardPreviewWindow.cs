#nullable enable
using System.Collections.Generic;
using System.Linq;
using ArcaneKingdom.Domain.Cards;
using UnityEditor;
using UnityEngine;

namespace ArcaneKingdom.EditorTools.Inspectors
{
    /// <summary>
    /// Editor-Fenster zur Inspektion aller Karten — sortierbar nach Element/Rarity,
    /// mit Stat-Vorschau, Fähigkeits-Slots und Deck-Limit-Anzeige.
    /// </summary>
    public sealed class CardPreviewWindow : EditorWindow
    {
        private Vector2 _scroll;
        private string _searchFilter = string.Empty;
        private Element? _elementFilter;
        private Rarity? _rarityFilter;
        private List<CardDefinition> _allCards = new();

        [MenuItem("ArcaneKingdom/Inspectors/Card Preview")]
        private static void Open()
        {
            GetWindow<CardPreviewWindow>("Cards").Refresh();
        }

        private void OnEnable()
        {
            // Defensive: NICHT direkt im OnEnable refreshen — kann crashen wenn das
            // Window beim Editor-Open noch im Layout liegt und Assets gerade
            // importiert/reloaded werden. DelayCall wartet bis stabile Editor-Phase.
            EditorApplication.delayCall += SafeRefresh;
        }

        private void SafeRefresh()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try { Refresh(); } catch (System.Exception ex) { Debug.LogWarning($"[CardPreview] Refresh skipped: {ex.Message}"); }
        }

        public void Refresh()
        {
            _allCards = AssetDatabase.FindAssets("t:CardDefinition")
                .Select(g => AssetDatabase.LoadAssetAtPath<CardDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null)
                .OrderBy(c => c!.Rarity)
                .ThenBy(c => c!.Element)
                .ThenBy(c => c!.Cost)
                .Select(c => c!)
                .ToList();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawCardsTable();
            DrawSummary();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70))) Refresh();
            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField);
            _elementFilter = EnumPopup(_elementFilter, "Alle Elemente");
            _rarityFilter = EnumPopup(_rarityFilter, "Alle Rarities");
            GUILayout.Label($"  {_allCards.Count} Karten total", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private static T? EnumPopup<T>(T? current, string nullLabel) where T : struct, System.Enum
        {
            var values = System.Enum.GetValues(typeof(T)).Cast<T>().ToArray();
            var labels = new[] { nullLabel }.Concat(values.Select(v => v.ToString())).ToArray();
            var selectedIdx = current.HasValue ? System.Array.IndexOf(values, current.Value) + 1 : 0;
            var newIdx = EditorGUILayout.Popup(selectedIdx, labels, EditorStyles.toolbarPopup, GUILayout.Width(130));
            return newIdx == 0 ? null : values[newIdx - 1];
        }

        private void DrawCardsTable()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var visible = _allCards.AsEnumerable();
            if (_elementFilter.HasValue) visible = visible.Where(c => c.Element == _elementFilter.Value);
            if (_rarityFilter.HasValue) visible = visible.Where(c => c.Rarity == _rarityFilter.Value);
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                var f = _searchFilter.ToLowerInvariant();
                visible = visible.Where(c => c.Id.ToLowerInvariant().Contains(f) || c.DisplayNameKey.ToLowerInvariant().Contains(f));
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            HeaderCell("ID", 200); HeaderCell("Element", 70); HeaderCell("Rarity", 90); HeaderCell("Race", 90);
            HeaderCell("Cost", 50); HeaderCell("ATK", 60); HeaderCell("HP", 60); HeaderCell("Turns", 50);
            HeaderCell("Limit", 90); HeaderCell("Abilities", 200);
            EditorGUILayout.EndHorizontal();

            foreach (var c in visible)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(c.Id, EditorStyles.linkLabel, GUILayout.Width(200))) Selection.activeObject = c;
                Cell(c.Element.ToString(), 70, ElementColor(c.Element));
                Cell(c.Rarity.ToString(), 90, RarityColor(c.Rarity));
                Cell(c.Race.ToString(), 90);
                Cell(c.Cost.ToString(), 50);
                Cell(c.BaseAttack.ToString("N0"), 60);
                Cell(c.BaseHealth.ToString("N0"), 60);
                Cell(c.TurnsToSpecial.ToString(), 50);
                Cell(c.DeckLimit.ToString(), 90);
                var abilities = string.Join(", ", new[] { c.BaseAbility?.Id, c.SecondAbility?.Id, c.ThirdAbility?.Id }.Where(a => !string.IsNullOrEmpty(a)));
                Cell(abilities, 200);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawSummary()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            foreach (Rarity r in System.Enum.GetValues(typeof(Rarity)))
            {
                var count = _allCards.Count(c => c.Rarity == r);
                GUILayout.Label($"{r}: {count}");
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void HeaderCell(string text, float width) =>
            GUILayout.Label(text, EditorStyles.toolbarButton, GUILayout.Width(width));

        private static void Cell(string text, float width, Color? color = null)
        {
            var style = new GUIStyle(EditorStyles.label) { fontSize = 11 };
            if (color.HasValue) style.normal.textColor = color.Value;
            GUILayout.Label(text, style, GUILayout.Width(width));
        }

        private static Color ElementColor(Element e) => e switch
        {
            Element.Natur  => new Color(0.30f, 0.78f, 0.30f),
            Element.Feuer  => new Color(0.90f, 0.35f, 0.20f),
            Element.Wasser => new Color(0.30f, 0.55f, 0.95f),
            Element.Licht  => new Color(0.95f, 0.85f, 0.40f),
            Element.Dunkel => new Color(0.55f, 0.30f, 0.65f),
            Element.Erde   => new Color(0.80f, 0.55f, 0.30f),
            _ => Color.white
        };

        private static Color RarityColor(Rarity r) => r switch
        {
            Rarity.Gewoehnlich   => Color.gray,
            Rarity.Ungewoehnlich => new Color(0.40f, 0.78f, 0.40f),
            Rarity.Selten        => new Color(0.40f, 0.65f, 0.95f),
            Rarity.Epic          => new Color(0.70f, 0.45f, 0.95f),
            Rarity.Legendaer     => new Color(0.95f, 0.78f, 0.30f),
            Rarity.Mythisch      => new Color(0.95f, 0.85f, 0.95f),
            _ => Color.white
        };
    }
}
