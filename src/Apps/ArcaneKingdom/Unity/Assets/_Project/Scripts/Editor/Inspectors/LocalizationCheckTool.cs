#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Runes;
using ArcaneKingdom.Domain.World;
using UnityEditor;
using UnityEngine;

namespace ArcaneKingdom.EditorTools.Inspectors
{
    /// <summary>
    /// Findet fehlende und unbenutzte Localization-Keys.
    ///
    /// Quellen fuer benutzte Keys:
    /// - Karten-, Faehigkeits-, Runen-, Welt-Definitions-SOs (displayNameKey, descriptionKey, etc.)
    ///
    /// Quelle fuer vorhandene Keys: Resources/Localization/strings.csv (1. Spalte).
    /// </summary>
    public sealed class LocalizationCheckTool : EditorWindow
    {
        private const string CsvPath = "Assets/_Project/Resources/Localization/strings.csv";

        private HashSet<string> _existing = new();
        private HashSet<string> _used = new();
        private Vector2 _scroll;
        private bool _showOnlyMissing = true;

        [MenuItem("ArcaneKingdom/Inspectors/Localization Check")]
        private static void Open() => GetWindow<LocalizationCheckTool>("Locales").Run();

        public void Run()
        {
            _existing = LoadExistingKeys();
            _used = CollectUsedKeys();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70))) Run();
            _showOnlyMissing = GUILayout.Toggle(_showOnlyMissing, "Nur Fehlend", EditorStyles.toolbarButton, GUILayout.Width(110));
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Existing: {_existing.Count}  |  Used: {_used.Count}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            var missing = _used.Except(_existing).OrderBy(k => k).ToList();
            var unused = _existing.Except(_used).Where(IsContentKey).OrderBy(k => k).ToList();

            EditorGUILayout.LabelField($"Fehlend ({missing.Count})", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var k in missing) EditorGUILayout.LabelField("  " + k);
            EditorGUILayout.EndScrollView();

            if (!_showOnlyMissing)
            {
                EditorGUILayout.LabelField($"Unbenutzt — Content-Keys ({unused.Count}):", EditorStyles.boldLabel);
                foreach (var k in unused) EditorGUILayout.LabelField("  " + k);
            }
        }

        private static HashSet<string> LoadExistingKeys()
        {
            var keys = new HashSet<string>();
            if (!File.Exists(CsvPath)) return keys;
            var first = true;
            foreach (var line in File.ReadAllLines(CsvPath))
            {
                if (first) { first = false; continue; }
                var firstComma = line.IndexOf(',');
                if (firstComma <= 0) continue;
                keys.Add(line.Substring(0, firstComma).Trim());
            }
            return keys;
        }

        private static HashSet<string> CollectUsedKeys()
        {
            var used = new HashSet<string>();
            foreach (var asset in AllOfType<CardDefinition>()) { used.Add(asset.DisplayNameKey); used.Add(asset.FlavorTextKey); }
            foreach (var asset in AllOfType<AbilityDefinition>()) { used.Add(asset.DisplayNameKey); used.Add(asset.DescriptionKey); }
            foreach (var asset in AllOfType<RuneDefinition>()) { used.Add(asset.DisplayNameKey); used.Add(asset.DescriptionKey); }
            foreach (var asset in AllOfType<WorldDefinition>())
            {
                used.Add(asset.DisplayNameKey);
                foreach (var node in asset.Nodes) used.Add(node.DisplayNameKey);
            }
            used.RemoveWhere(string.IsNullOrEmpty);
            return used;
        }

        private static IEnumerable<T> AllOfType<T>() where T : ScriptableObject
        {
            return AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null);
        }

        /// <summary>
        /// Filter: zeigt nur "Content"-Keys an (Karten, Faehigkeiten, etc.) statt UI/Element-Keys.
        /// </summary>
        private static bool IsContentKey(string key) =>
            key.StartsWith("card.") || key.StartsWith("ability.") || key.StartsWith("rune.") || key.StartsWith("world.");
    }
}
