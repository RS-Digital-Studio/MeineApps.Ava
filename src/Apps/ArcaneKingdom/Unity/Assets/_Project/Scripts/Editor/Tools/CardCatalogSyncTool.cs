#nullable enable
using System.IO;
using System.Linq;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Game.Catalog;
using UnityEditor;
using UnityEngine;

namespace ArcaneKingdom.EditorTools
{
    /// <summary>
    /// Editor-Menu "ArcaneKingdom > Tools > Sync CardCatalog". Findet alle
    /// <see cref="CardDefinition"/>-Assets im Projekt und schreibt sie in das
    /// CardCatalog.asset unter Assets/_Project/Resources/.
    ///
    /// Muss aufgerufen werden:
    ///   - Nach jedem Import neuer Cards (DataImporter)
    ///   - Wenn man neue Karten haendisch erstellt hat
    ///   - Vor jedem Build (sonst sind Karten im Build nicht verfuegbar)
    /// </summary>
    public static class CardCatalogSyncTool
    {
        private const string CatalogPath = "Assets/_Project/Resources/CardCatalog.asset";

        [MenuItem("ArcaneKingdom/Tools/Sync CardCatalog")]
        public static void Sync()
        {
            // Resources-Folder sicherstellen
            var dir = Path.GetDirectoryName(CatalogPath)!.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
            {
                var parts = dir.Split('/');
                var current = parts[0];
                for (var i = 1; i < parts.Length; i++)
                {
                    var next = $"{current}/{parts[i]}";
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }

            // Alle CardDefinitions im Projekt finden
            var cards = AssetDatabase.FindAssets("t:CardDefinition")
                .Select(g => AssetDatabase.LoadAssetAtPath<CardDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null)
                .OrderBy(c => c!.Rarity)
                .ThenBy(c => c!.Element)
                .ThenBy(c => c!.Id)
                .ToArray();

            // Catalog laden oder erzeugen
            var catalog = AssetDatabase.LoadAssetAtPath<CardCatalogAsset>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CardCatalogAsset>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
                Debug.Log($"[CardCatalog] Neues Catalog angelegt unter {CatalogPath}");
            }

            catalog.SetCards(cards!);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CardCatalog] Sync abgeschlossen — {cards.Length} Karten registriert.");
        }
    }
}
