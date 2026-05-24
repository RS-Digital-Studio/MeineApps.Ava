#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Hero;
using ArcaneKingdom.Domain.Runes;
using ArcaneKingdom.Domain.World;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace ArcaneKingdom.EditorTools.Data
{
    /// <summary>
    /// Konvertiert JSON-Definitionen aus <c>Assets/_Project/Resources/Data/</c> in
    /// ScriptableObject-Assets unter <c>Assets/_Project/ScriptableObjects/</c>.
    /// Validierung erfolgt vor dem Schreiben — bei Fehler wird abgebrochen.
    /// </summary>
    public static class DataImporter
    {
        private const string DataFolderRel = "Assets/_Project/Resources/Data";
        private const string SoRootRel = "Assets/_Project/ScriptableObjects";

        [MenuItem("ArcaneKingdom/Data/Import All")]
        public static void ImportAll()
        {
            try
            {
                AssetDatabase.StartAssetEditing();
                var abilities = ImportAbilities();
                var cards = ImportCards(abilities);
                ImportRunes();
                ImportWorlds(cards);
                ImportHeroes();
                ImportBalancing();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            Debug.Log("[DataImporter] Alle Daten importiert.");
        }

        [MenuItem("ArcaneKingdom/Data/Import Abilities")]
        public static void ImportAbilitiesMenu() { ImportAbilities(); AssetDatabase.SaveAssets(); }

        [MenuItem("ArcaneKingdom/Data/Import Cards")]
        public static void ImportCardsMenu()
        {
            var abilities = LoadExistingAbilities();
            ImportCards(abilities); AssetDatabase.SaveAssets();
        }

        // ----------------------------------------------------------- Abilities

        private static Dictionary<string, AbilityDefinition> ImportAbilities()
        {
            var json = File.ReadAllText(Path.Combine(DataFolderRel, "abilities.json"));
            var dtos = JsonConvert.DeserializeObject<List<AbilityDto>>(json) ?? new();
            var ids = new HashSet<string>();
            var result = new Dictionary<string, AbilityDefinition>();

            foreach (var dto in dtos)
            {
                if (!ids.Add(dto.id)) throw new Exception($"Abilities: Doppelte ID '{dto.id}'");
                var so = LoadOrCreateAsset<AbilityDefinition>($"{SoRootRel}/Abilities/Ability_{dto.id}.asset");
                ApplyAbility(so, dto);
                EditorUtility.SetDirty(so);
                result[dto.id] = so;
            }
            Debug.Log($"[DataImporter] {result.Count} Faehigkeiten importiert.");
            return result;
        }

        private static Dictionary<string, AbilityDefinition> LoadExistingAbilities()
        {
            var dict = new Dictionary<string, AbilityDefinition>();
            var guids = AssetDatabase.FindAssets("t:AbilityDefinition", new[] { $"{SoRootRel}/Abilities" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
                if (asset != null) dict[asset.Id] = asset;
            }
            return dict;
        }

        // ----------------------------------------------------------- Cards

        private static Dictionary<string, CardDefinition> ImportCards(IReadOnlyDictionary<string, AbilityDefinition> abilities)
        {
            var json = File.ReadAllText(Path.Combine(DataFolderRel, "cards.json"));
            var dtos = JsonConvert.DeserializeObject<List<CardDto>>(json) ?? new();
            var ids = new HashSet<string>();
            var result = new Dictionary<string, CardDefinition>();

            foreach (var dto in dtos)
            {
                if (!ids.Add(dto.id)) throw new Exception($"Cards: Doppelte ID '{dto.id}'");
                ValidateCard(dto, abilities);

                var so = LoadOrCreateAsset<CardDefinition>($"{SoRootRel}/Cards/Card_{dto.id}.asset");
                ApplyCard(so, dto, abilities);
                EditorUtility.SetDirty(so);
                result[dto.id] = so;
            }
            Debug.Log($"[DataImporter] {result.Count} Karten importiert.");
            return result;
        }

        private static void ValidateCard(CardDto dto, IReadOnlyDictionary<string, AbilityDefinition> abilities)
        {
            if (dto.cost < 1 || dto.cost > 10) throw new Exception($"Card '{dto.id}': Cost {dto.cost} ausserhalb 1-10.");
            if (dto.baseAttack < 0) throw new Exception($"Card '{dto.id}': BaseAttack negativ.");
            if (dto.baseHealth < 1) throw new Exception($"Card '{dto.id}': BaseHealth < 1.");
            if (dto.turnsToSpecial < 1 || dto.turnsToSpecial > 10)
                throw new Exception($"Card '{dto.id}': TurnsToSpecial {dto.turnsToSpecial} ausserhalb 1-10.");
            CheckAbility(dto.id, dto.baseAbilityId, abilities, required: true);
            CheckAbility(dto.id, dto.secondAbilityId, abilities, required: false);
            CheckAbility(dto.id, dto.thirdAbilityId, abilities, required: false);
        }

        private static void CheckAbility(string cardId, string? abilityId, IReadOnlyDictionary<string, AbilityDefinition> abilities, bool required)
        {
            if (string.IsNullOrEmpty(abilityId))
            {
                if (required) throw new Exception($"Card '{cardId}': Basis-Faehigkeit ist Pflicht.");
                return;
            }
            if (!abilities.ContainsKey(abilityId!))
                throw new Exception($"Card '{cardId}': Faehigkeit '{abilityId}' nicht in abilities.json.");
        }

        // ----------------------------------------------------------- Runes

        private static void ImportRunes()
        {
            var json = File.ReadAllText(Path.Combine(DataFolderRel, "runes.json"));
            var dtos = JsonConvert.DeserializeObject<List<RuneDto>>(json) ?? new();
            var ids = new HashSet<string>();

            foreach (var dto in dtos)
            {
                if (!ids.Add(dto.id)) throw new Exception($"Runes: Doppelte ID '{dto.id}'");
                var so = LoadOrCreateAsset<RuneDefinition>($"{SoRootRel}/Runes/Rune_{dto.id}.asset");
                ApplyRune(so, dto);
                EditorUtility.SetDirty(so);
            }
            Debug.Log($"[DataImporter] {dtos.Count} Runen importiert.");
        }

        // ----------------------------------------------------------- Worlds

        private static void ImportWorlds(IReadOnlyDictionary<string, CardDefinition> cards)
        {
            var json = File.ReadAllText(Path.Combine(DataFolderRel, "worlds.json"));
            var dtos = JsonConvert.DeserializeObject<List<WorldDto>>(json) ?? new();
            var ids = new HashSet<string>();

            foreach (var dto in dtos)
            {
                if (!ids.Add(dto.id)) throw new Exception($"Worlds: Doppelte ID '{dto.id}'");
                ValidateWorld(dto, cards);

                var so = LoadOrCreateAsset<WorldDefinition>($"{SoRootRel}/Worlds/World_{dto.id}.asset");
                ApplyWorld(so, dto);
                EditorUtility.SetDirty(so);
            }
            Debug.Log($"[DataImporter] {dtos.Count} Welten importiert.");
        }

        private static void ValidateWorld(WorldDto dto, IReadOnlyDictionary<string, CardDefinition> cards)
        {
            if (dto.nodes == null || dto.nodes.Count != 10)
                throw new Exception($"World '{dto.id}': Muss genau 10 Nodes haben (hat {dto.nodes?.Count ?? 0}).");
            foreach (var n in dto.nodes)
                foreach (var cId in n.enemyDeckCardIds ?? Array.Empty<string>())
                    if (!cards.ContainsKey(cId))
                        throw new Exception($"World '{dto.id}', Node '{n.id}': Karte '{cId}' unbekannt.");
        }

        // ----------------------------------------------------------- Heroes

        [MenuItem("ArcaneKingdom/Data/Import Heroes")]
        public static void ImportHeroesMenu() { ImportHeroes(); AssetDatabase.SaveAssets(); }

        private static void ImportHeroes()
        {
            var path = Path.Combine(DataFolderRel, "heroes.json");
            if (!File.Exists(path)) { Debug.LogWarning("[DataImporter] heroes.json fehlt — Import uebersprungen."); return; }
            var json = File.ReadAllText(path);
            var dtos = JsonConvert.DeserializeObject<List<HeroDto>>(json) ?? new();
            var ids = new HashSet<string>();

            foreach (var dto in dtos)
            {
                if (!ids.Add(dto.id)) throw new Exception($"Heroes: Doppelte ID '{dto.id}'");
                var so = LoadOrCreateAsset<HeroDefinition>($"{SoRootRel}/Heroes/Hero_{dto.id}.asset");
                ApplyHero(so, dto);
                EditorUtility.SetDirty(so);
            }
            Debug.Log($"[DataImporter] {dtos.Count} Helden importiert.");
        }

        private static void ApplyHero(HeroDefinition so, HeroDto dto)
        {
            var sObj = new SerializedObject(so);
            sObj.FindProperty("id").stringValue = dto.id;
            sObj.FindProperty("displayNameKey").stringValue = dto.displayNameKey;
            sObj.FindProperty("flavorTextKey").stringValue = dto.flavorTextKey;
            sObj.FindProperty("element").enumValueIndex = (int)dto.element;
            sObj.FindProperty("faehigkeitNameKey").stringValue = dto.faehigkeitNameKey;
            sObj.FindProperty("faehigkeitDescKey").stringValue = dto.faehigkeitDescKey;
            sObj.FindProperty("faehigkeitsTyp").enumValueIndex = (int)dto.faehigkeitsTyp;
            sObj.FindProperty("cooldownRunden").intValue = dto.cooldownRunden;
            sObj.FindProperty("magnitude").intValue = dto.magnitude;
            sObj.FindProperty("durationTurns").intValue = dto.durationTurns;
            sObj.FindProperty("portraitAddressableKey").stringValue = dto.portraitAddressableKey ?? string.Empty;
            sObj.FindProperty("voiceLineAddressableKey").stringValue = dto.voiceLineAddressableKey ?? string.Empty;
            sObj.ApplyModifiedPropertiesWithoutUndo();
        }

        // ----------------------------------------------------------- Balancing

        private static void ImportBalancing()
        {
            // BalancingConfig hat zur Zeit nur primitive Werte — wir lesen die JSON
            // und schreiben ueber Reflection / SerializedObject. Im Erst-Wurf
            // platzieren wir das Asset und lassen Werte aus dem Inspector setzen.
            var path = $"{SoRootRel}/Config/BalancingConfig.asset";
            LoadOrCreateAsset<Domain.Config.BalancingConfig>(path);
            Debug.Log("[DataImporter] BalancingConfig-Asset vorhanden (Werte via Inspector pflegen).");
        }

        // ----------------------------------------------------------- Helpers

        private static T LoadOrCreateAsset<T>(string assetPath) where T : ScriptableObject
        {
            // Auf Windows liefert Path.GetDirectoryName Backslashes, AssetDatabase
            // erwartet aber Forward-Slashes — explizit normalisieren.
            var dir = (Path.GetDirectoryName(assetPath) ?? string.Empty).Replace('\\', '/');
            EnsureFolder(dir);
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null) return existing;
            var instance = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(instance, assetPath);
            return instance;
        }

        private static void EnsureFolder(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return;
            relativePath = relativePath.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(relativePath)) return;
            var parts = relativePath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void ApplyAbility(AbilityDefinition so, AbilityDto dto)
        {
            var sObj = new SerializedObject(so);
            sObj.FindProperty("id").stringValue = dto.id;
            sObj.FindProperty("displayNameKey").stringValue = dto.displayNameKey;
            sObj.FindProperty("descriptionKey").stringValue = dto.descriptionKey;
            sObj.FindProperty("type").enumValueIndex = (int)dto.type;
            sObj.FindProperty("category").enumValueIndex = (int)dto.category;
            sObj.FindProperty("magnitude").intValue = dto.magnitude;
            sObj.FindProperty("durationTurns").intValue = dto.durationTurns;
            sObj.FindProperty("targetsAllAllies").boolValue = dto.targetsAllAllies;
            sObj.FindProperty("targetsAllEnemies").boolValue = dto.targetsAllEnemies;
            sObj.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyCard(CardDefinition so, CardDto dto, IReadOnlyDictionary<string, AbilityDefinition> abilities)
        {
            var sObj = new SerializedObject(so);
            sObj.FindProperty("id").stringValue = dto.id;
            sObj.FindProperty("displayNameKey").stringValue = dto.displayNameKey;
            sObj.FindProperty("flavorTextKey").stringValue = dto.flavorTextKey;
            sObj.FindProperty("element").enumValueIndex = (int)dto.element;
            sObj.FindProperty("rarity").enumValueIndex = (int)dto.rarity;
            sObj.FindProperty("race").enumValueIndex = (int)dto.race;
            sObj.FindProperty("cost").intValue = dto.cost;
            sObj.FindProperty("baseAttack").intValue = dto.baseAttack;
            sObj.FindProperty("baseHealth").intValue = dto.baseHealth;
            sObj.FindProperty("turnsToSpecial").intValue = dto.turnsToSpecial;
            sObj.FindProperty("deckLimit").enumValueIndex = (int)dto.deckLimit;
            sObj.FindProperty("globalCraftLimit").intValue = dto.globalCraftLimit;
            sObj.FindProperty("artworkAddressableKey").stringValue = dto.artworkAddressableKey ?? string.Empty;
            sObj.FindProperty("voiceLineAddressableKey").stringValue = dto.voiceLineAddressableKey ?? string.Empty;
            sObj.FindProperty("baseAbility").objectReferenceValue = dto.baseAbilityId != null && abilities.TryGetValue(dto.baseAbilityId, out var a1) ? a1 : null;
            sObj.FindProperty("secondAbility").objectReferenceValue = dto.secondAbilityId != null && abilities.TryGetValue(dto.secondAbilityId, out var a2) ? a2 : null;
            sObj.FindProperty("thirdAbility").objectReferenceValue = dto.thirdAbilityId != null && abilities.TryGetValue(dto.thirdAbilityId, out var a3) ? a3 : null;
            sObj.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyRune(RuneDefinition so, RuneDto dto)
        {
            var sObj = new SerializedObject(so);
            sObj.FindProperty("id").stringValue = dto.id;
            sObj.FindProperty("displayNameKey").stringValue = dto.displayNameKey;
            sObj.FindProperty("descriptionKey").stringValue = dto.descriptionKey;
            sObj.FindProperty("type").enumValueIndex = (int)dto.type;
            sObj.FindProperty("rarity").enumValueIndex = (int)dto.rarity;
            sObj.FindProperty("baseMagnitude").floatValue = dto.baseMagnitude;
            sObj.FindProperty("magnitudePerLevel").floatValue = dto.magnitudePerLevel;
            sObj.FindProperty("elementTarget").enumValueIndex = (int)dto.elementTarget;
            sObj.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyWorld(WorldDefinition so, WorldDto dto)
        {
            var sObj = new SerializedObject(so);
            sObj.FindProperty("id").stringValue = dto.id;
            sObj.FindProperty("displayNameKey").stringValue = dto.displayNameKey;
            sObj.FindProperty("index").intValue = dto.index;
            sObj.FindProperty("themeElement").enumValueIndex = (int)dto.themeElement;
            sObj.FindProperty("recommendedPlayerLevel").intValue = dto.recommendedPlayerLevel;
            sObj.FindProperty("backgroundAddressableKey").stringValue = dto.backgroundAddressableKey ?? string.Empty;
            sObj.FindProperty("musicAddressableKey").stringValue = dto.musicAddressableKey ?? string.Empty;

            var nodesProp = sObj.FindProperty("nodes");
            nodesProp.arraySize = dto.nodes!.Count;
            for (var i = 0; i < dto.nodes.Count; i++)
            {
                var n = dto.nodes[i];
                var elem = nodesProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("id").stringValue = n.id;
                elem.FindPropertyRelative("displayNameKey").stringValue = n.displayNameKey;
                elem.FindPropertyRelative("nodeIndex").intValue = n.nodeIndex;
                elem.FindPropertyRelative("type").enumValueIndex = (int)n.type;
                var deckProp = elem.FindPropertyRelative("enemyDeckCardIds");
                deckProp.arraySize = (n.enemyDeckCardIds ?? Array.Empty<string>()).Length;
                for (var j = 0; j < deckProp.arraySize; j++)
                    deckProp.GetArrayElementAtIndex(j).stringValue = n.enemyDeckCardIds![j];
                elem.FindPropertyRelative("goldOneStar").intValue = n.goldOneStar;
                elem.FindPropertyRelative("goldTwoStar").intValue = n.goldTwoStar;
                elem.FindPropertyRelative("goldThreeStar").intValue = n.goldThreeStar;
                elem.FindPropertyRelative("goldFourStar").intValue = n.goldFourStar;
                elem.FindPropertyRelative("expOneStar").intValue = n.expOneStar;
                elem.FindPropertyRelative("expTwoStar").intValue = n.expTwoStar;
                elem.FindPropertyRelative("expThreeStar").intValue = n.expThreeStar;
                elem.FindPropertyRelative("expFourStar").intValue = n.expFourStar;
            }

            sObj.ApplyModifiedPropertiesWithoutUndo();
        }

        // ----------------------------------------------------------- DTOs

        [Serializable]
        private sealed class AbilityDto
        {
            public string id = string.Empty;
            public string displayNameKey = string.Empty;
            public string descriptionKey = string.Empty;
            public AbilityType type = AbilityType.Passive;
            public AbilityCategory category = AbilityCategory.Damage;
            public int magnitude;
            public int durationTurns;
            public bool targetsAllAllies;
            public bool targetsAllEnemies;
        }

        [Serializable]
        private sealed class CardDto
        {
            public string id = string.Empty;
            public string displayNameKey = string.Empty;
            public string flavorTextKey = string.Empty;
            public Element element = Element.Natur;
            public Rarity rarity = Rarity.Gewoehnlich;
            public Race race = Race.Koenigreich;
            public int cost = 1;
            public int baseAttack = 100;
            public int baseHealth = 200;
            public int turnsToSpecial = 4;
            public string? baseAbilityId;
            public string? secondAbilityId;
            public string? thirdAbilityId;
            public DeckLimit deckLimit = DeckLimit.Unlimited;
            public int globalCraftLimit = 90;
            public string? artworkAddressableKey;
            public string? voiceLineAddressableKey;
        }

        [Serializable]
        private sealed class RuneDto
        {
            public string id = string.Empty;
            public string displayNameKey = string.Empty;
            public string descriptionKey = string.Empty;
            public RuneType type = RuneType.Angriff;
            public Rarity rarity = Rarity.Gewoehnlich;
            public float baseMagnitude = 5f;
            public float magnitudePerLevel = 1f;
            public Element elementTarget = Element.Natur;
        }

        [Serializable]
        private sealed class HeroDto
        {
            public string id = string.Empty;
            public string displayNameKey = string.Empty;
            public string flavorTextKey = string.Empty;
            public Element element = Element.Licht;
            public string faehigkeitNameKey = string.Empty;
            public string faehigkeitDescKey = string.Empty;
            public HeroFaehigkeitsTyp faehigkeitsTyp = HeroFaehigkeitsTyp.AllyHeal;
            public int cooldownRunden = 5;
            public int magnitude;
            public int durationTurns;
            public string? portraitAddressableKey;
            public string? voiceLineAddressableKey;
        }

        [Serializable]
        private sealed class WorldDto
        {
            public string id = string.Empty;
            public string displayNameKey = string.Empty;
            public int index = 1;
            public Element themeElement = Element.Natur;
            public int recommendedPlayerLevel = 1;
            public string? backgroundAddressableKey;
            public string? musicAddressableKey;
            public List<NodeDto>? nodes;
        }

        [Serializable]
        private sealed class NodeDto
        {
            public string id = string.Empty;
            public string displayNameKey = string.Empty;
            public int nodeIndex = 1;
            public NodeType type = NodeType.Normal;
            public string[]? enemyDeckCardIds;
            public int goldOneStar = 50;
            public int goldTwoStar = 100;
            public int goldThreeStar = 200;
            public int goldFourStar = 500;
            public int expOneStar = 10;
            public int expTwoStar = 25;
            public int expThreeStar = 50;
            public int expFourStar = 100;
        }
    }
}
