#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// Angepasst auf Designplan v4 (5 Rassen, 6 Elemente Doppel-Dreieck, 6 Seltenheitsstufen).
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
                var nodeIds = ImportWorlds(cards);
                ImportHeroes();
                ImportBalancing();

                // Querverweis-Validierung der bislang reinen Daten-Dateien (kein SO-Import noetig).
                // Verstoesse werden geloggt, damit fehlende/falsche Referenzen frueh auffallen.
                ValidateCrossReferences(cards, nodeIds);
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
            if (dto.cost < 1 || dto.cost > 60) throw new Exception($"Card '{dto.id}': Cost {dto.cost} ausserhalb 1-60 (Designplan v4: 1*~5 bis 6*~50).");
            if (dto.baseAttack < 0) throw new Exception($"Card '{dto.id}': BaseAttack negativ.");
            if (dto.baseHealth < 1) throw new Exception($"Card '{dto.id}': BaseHealth < 1.");
            if (dto.turnsToSpecial < 1 || dto.turnsToSpecial > 10)
                throw new Exception($"Card '{dto.id}': TurnsToSpecial {dto.turnsToSpecial} ausserhalb 1-10.");

            // Goetter sind nur 4*+ und nicht als Drop erhaeltlich (Designplan v4 Kap. 2).
            if (dto.race == Race.Goetter && dto.rarity < Rarity.Epic)
                throw new Exception($"Card '{dto.id}': Goetter-Karten muessen mindestens 4* (Epic) sein.");

            // Mythische Karten brauchen einen Letzten Willen (Designplan v4 Kap. 4.2).
            if (dto.rarity == Rarity.Mythisch && string.IsNullOrEmpty(dto.lastWillAbilityId))
                throw new Exception($"Card '{dto.id}': 6* Mythische Karten muessen einen lastWillAbilityId haben.");

            // Premium-Karten duerfen nicht in Fusion verwendet werden (Designplan v4 Kap. 3 Oeko).
            // Hier nur Konsistenz-Check — Logik liegt im Crafting-Service.

            // baseAbility nur Pflicht falls keine reine Oekosystem-Karte (Event/Premium koennen einfacher sein).
            CheckAbility(dto.id, dto.baseAbilityId, abilities, required: true);
            CheckAbility(dto.id, dto.secondAbilityId, abilities, required: false);
            CheckAbility(dto.id, dto.thirdAbilityId, abilities, required: false);
            CheckAbility(dto.id, dto.lastWillAbilityId, abilities, required: false);
        }

        private static void CheckAbility(string cardId, string? abilityId, IReadOnlyDictionary<string, AbilityDefinition> abilities, bool required)
        {
            if (string.IsNullOrEmpty(abilityId))
            {
                if (required) throw new Exception($"Card '{cardId}': Basis-Faehigkeit ist Pflicht.");
                return;
            }
            // In v4 sind viele Skill-IDs noch Platzhalter — wir warnen nur, brechen aber nicht ab.
            if (!abilities.ContainsKey(abilityId!))
                Debug.LogWarning($"Card '{cardId}': Faehigkeit '{abilityId}' nicht in abilities.json (wird beim Import zu null).");
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

        private static HashSet<string> ImportWorlds(IReadOnlyDictionary<string, CardDefinition> cards)
        {
            var json = File.ReadAllText(Path.Combine(DataFolderRel, "worlds.json"));
            var dtos = JsonConvert.DeserializeObject<List<WorldDto>>(json) ?? new();
            var ids = new HashSet<string>();
            var nodeIds = new HashSet<string>();

            foreach (var dto in dtos)
            {
                if (!ids.Add(dto.id)) throw new Exception($"Worlds: Doppelte ID '{dto.id}'");
                ValidateWorld(dto, cards);

                foreach (var n in dto.nodes ?? new List<NodeDto>())
                    if (!string.IsNullOrEmpty(n.id)) nodeIds.Add(n.id);

                var so = LoadOrCreateAsset<WorldDefinition>($"{SoRootRel}/Worlds/World_{dto.id}.asset");
                ApplyWorld(so, dto);
                EditorUtility.SetDirty(so);
            }
            Debug.Log($"[DataImporter] {dtos.Count} Welten importiert.");
            return nodeIds;
        }

        private static void ValidateWorld(WorldDto dto, IReadOnlyDictionary<string, CardDefinition> cards)
        {
            if (dto.nodes == null || dto.nodes.Count != 10)
                throw new Exception($"World '{dto.id}': Muss genau 10 Nodes haben (hat {dto.nodes?.Count ?? 0}).");
            foreach (var n in dto.nodes)
                foreach (var cId in n.enemyDeckCardIds ?? Array.Empty<string>())
                    if (!cards.ContainsKey(cId))
                        throw new Exception($"World '{dto.id}', Node '{n.id}': Karte '{cId}' unbekannt.");
            if (!string.IsNullOrEmpty(dto.bossCardId) && !cards.ContainsKey(dto.bossCardId!))
                Debug.LogWarning($"World '{dto.id}': bossCardId '{dto.bossCardId}' nicht in cards.json.");
            if (!string.IsNullOrEmpty(dto.prestige4CardId) && !cards.ContainsKey(dto.prestige4CardId!))
                Debug.LogWarning($"World '{dto.id}': prestige4CardId '{dto.prestige4CardId}' nicht in cards.json.");
        }

        // ----------------------------------------------------------- Querverweis-Validierung (reine Daten-Dateien)

        /// <summary>
        /// Prueft die Daten-Dateien, die keinen eigenen ScriptableObject-Import haben, auf
        /// konsistente Karten- und Node-Referenzen. Harte Verstoesse (Datenverlust, falsche
        /// Spielregeln) werfen; weichere Inkonsistenzen werden nur als Warnung geloggt.
        /// </summary>
        private static void ValidateCrossReferences(IReadOnlyDictionary<string, CardDefinition> cards, IReadOnlyCollection<string> nodeIds)
        {
            ValidateCollections(cards);
            ValidateFusionRecipes(cards);
            ValidateMaterialDrops(nodeIds);
            ValidatePremiumShop(cards);
            ValidateStarTemple(cards);
            ValidateEvents(cards);
        }

        private static List<T> LoadJsonArray<T>(string fileName)
        {
            var path = Path.Combine(DataFolderRel, fileName);
            if (!File.Exists(path)) { Debug.LogWarning($"[DataImporter] {fileName} fehlt — Validierung uebersprungen."); return new List<T>(); }
            return JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(path)) ?? new List<T>();
        }

        private static T? LoadJsonObject<T>(string fileName) where T : class
        {
            var path = Path.Combine(DataFolderRel, fileName);
            if (!File.Exists(path)) { Debug.LogWarning($"[DataImporter] {fileName} fehlt — Validierung uebersprungen."); return null; }
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
        }

        private static void ValidateCollections(IReadOnlyDictionary<string, CardDefinition> cards)
        {
            var dtos = LoadJsonArray<CollectionDto>("collections.json");
            foreach (var dto in dtos)
            {
                // rewardCardId ist Pflicht und MUSS in cards.json existieren (sonst Datenverlust beim Einloesen des Sets).
                if (string.IsNullOrEmpty(dto.rewardCardId))
                    throw new Exception($"Collection '{dto.id}': rewardCardId fehlt.");
                if (!cards.ContainsKey(dto.rewardCardId!))
                    throw new Exception($"Collection '{dto.id}': rewardCardId '{dto.rewardCardId}' existiert nicht in cards.json.");
                if (dto.requiredMaterialIds == null || dto.requiredMaterialIds.Count == 0)
                    Debug.LogWarning($"Collection '{dto.id}': Keine requiredMaterialIds definiert.");
            }
            Debug.Log($"[DataImporter] {dtos.Count} Sammel-Sets validiert.");
        }

        private static void ValidateFusionRecipes(IReadOnlyDictionary<string, CardDefinition> cards)
        {
            var dtos = LoadJsonArray<FusionRecipeDto>("fusion_recipes.json");
            var recipeIds = new HashSet<string>();
            foreach (var dto in dtos)
            {
                if (!string.IsNullOrEmpty(dto.id) && !recipeIds.Add(dto.id!))
                    throw new Exception($"FusionRecipes: Doppelte ID '{dto.id}'.");

                // Ergebnis-Karte muss existieren.
                if (string.IsNullOrEmpty(dto.resultCardId) || !cards.ContainsKey(dto.resultCardId!))
                    throw new Exception($"FusionRecipe '{dto.id}': resultCardId '{dto.resultCardId}' existiert nicht in cards.json.");

                // Input-Karten muessen existieren UND duerfen keine Premium-Karten sein
                // (Premium-Karten koennen nicht in Fusion verwendet werden — sonst Verlust einer gekauften Karte).
                foreach (var inId in dto.requiredCardIds ?? new List<string>())
                {
                    if (!cards.TryGetValue(inId, out var inCard))
                        throw new Exception($"FusionRecipe '{dto.id}': Input-Karte '{inId}' existiert nicht in cards.json.");
                    if (inCard.IsPremiumCard)
                        throw new Exception($"FusionRecipe '{dto.id}': Premium-Karte '{inId}' darf NICHT als Fusion-Input verwendet werden.");
                }
            }
            Debug.Log($"[DataImporter] {dtos.Count} Fusions-Rezepte validiert.");
        }

        private static void ValidateMaterialDrops(IReadOnlyCollection<string> nodeIds)
        {
            var dtos = LoadJsonArray<MaterialDropDto>("material_drops.json");
            foreach (var dto in dtos)
            {
                if (string.IsNullOrEmpty(dto.nodeId))
                    throw new Exception("MaterialDrops: Eintrag ohne nodeId.");
                if (!nodeIds.Contains(dto.nodeId!))
                    Debug.LogWarning($"MaterialDrops: nodeId '{dto.nodeId}' kommt in keiner Welt aus worlds.json vor.");
                foreach (var d in dto.drops ?? new List<MaterialDropEntryDto>())
                    if (string.IsNullOrEmpty(d.materialId))
                        Debug.LogWarning($"MaterialDrops '{dto.nodeId}': Drop ohne materialId.");
            }
            Debug.Log($"[DataImporter] {dtos.Count} Material-Drop-Eintraege validiert.");
        }

        private static void ValidatePremiumShop(IReadOnlyDictionary<string, CardDefinition> cards)
        {
            var dto = LoadJsonObject<PremiumShopDto>("premium_shop.json");
            if (dto == null) return;
            void Check(PremiumShopSlotDto? slot, string section)
            {
                if (slot == null) return;
                if (string.IsNullOrEmpty(slot.cardId) || !cards.TryGetValue(slot.cardId!, out var card))
                    throw new Exception($"PremiumShop ({section}): cardId '{slot.cardId}' existiert nicht in cards.json.");
                if (!card.IsPremiumCard)
                    Debug.LogWarning($"PremiumShop ({section}): Karte '{slot.cardId}' ist nicht als isPremiumCard markiert.");
            }
            foreach (var s in dto.permanentSlots ?? new List<PremiumShopSlotDto>()) Check(s, "permanent");
            foreach (var s in dto.rotatingSlots ?? new List<PremiumShopSlotDto>()) Check(s, "rotating");
            Debug.Log("[DataImporter] Premium-Shop validiert.");
        }

        private static void ValidateStarTemple(IReadOnlyDictionary<string, CardDefinition> cards)
        {
            var dto = LoadJsonObject<StarTempleDto>("star_temple.json");
            if (dto == null) return;
            foreach (var ex in dto.exchanges ?? new List<StarTempleExchangeDto>())
            {
                // rewardCardId ist nur bei konkreten Karten-Belohnungen gesetzt.
                if (!string.IsNullOrEmpty(ex.rewardCardId) && !cards.ContainsKey(ex.rewardCardId!))
                    throw new Exception($"StarTemple '{ex.id}': rewardCardId '{ex.rewardCardId}' existiert nicht in cards.json.");
            }
            Debug.Log("[DataImporter] Sternkarten-Tempel validiert.");
        }

        private static void ValidateEvents(IReadOnlyDictionary<string, CardDefinition> cards)
        {
            var dtos = LoadJsonArray<EventDto>("events.json");
            foreach (var dto in dtos)
            {
                foreach (var ec in dto.eventCards ?? new List<EventCardDto>())
                {
                    if (string.IsNullOrEmpty(ec.cardId) || !cards.TryGetValue(ec.cardId!, out var card))
                        throw new Exception($"Event '{dto.id}': eventCard '{ec.cardId}' existiert nicht in cards.json.");
                    if (!card.IsEventCard)
                        Debug.LogWarning($"Event '{dto.id}': Karte '{ec.cardId}' ist nicht als isEventCard markiert.");
                }
            }
            Debug.Log($"[DataImporter] {dtos.Count} Events validiert.");
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
            sObj.FindProperty("race").enumValueIndex = (int)dto.race;
            sObj.FindProperty("faehigkeitNameKey").stringValue = dto.faehigkeitNameKey;
            sObj.FindProperty("faehigkeitDescKey").stringValue = dto.faehigkeitDescKey;
            sObj.FindProperty("faehigkeitsTyp").enumValueIndex = (int)dto.faehigkeitsTyp;
            sObj.FindProperty("magnitude").intValue = dto.magnitude;
            sObj.FindProperty("portraitAddressableKey").stringValue = dto.portraitAddressableKey ?? string.Empty;
            sObj.FindProperty("voiceLineAddressableKey").stringValue = dto.voiceLineAddressableKey ?? string.Empty;
            sObj.ApplyModifiedPropertiesWithoutUndo();
        }

        // ----------------------------------------------------------- Balancing

        private static void ImportBalancing()
        {
            var path = $"{SoRootRel}/Config/BalancingConfig.asset";
            LoadOrCreateAsset<Domain.Config.BalancingConfig>(path);
            Debug.Log("[DataImporter] BalancingConfig-Asset vorhanden (Werte via Inspector pflegen).");
        }

        // ----------------------------------------------------------- Helpers

        private static T LoadOrCreateAsset<T>(string assetPath) where T : ScriptableObject
        {
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

            sObj.FindProperty("baseAbility").objectReferenceValue   = ResolveAbility(dto.baseAbilityId, abilities);
            sObj.FindProperty("secondAbility").objectReferenceValue = ResolveAbility(dto.secondAbilityId, abilities);
            sObj.FindProperty("thirdAbility").objectReferenceValue  = ResolveAbility(dto.thirdAbilityId, abilities);
            sObj.FindProperty("lastWillAbility").objectReferenceValue = ResolveAbility(dto.lastWillAbilityId, abilities);

            // Personality-Lines
            sObj.FindProperty("onPlayLineKey").stringValue    = dto.onPlayLineKey ?? string.Empty;
            sObj.FindProperty("onVictoryLineKey").stringValue = dto.onVictoryLineKey ?? string.Empty;
            sObj.FindProperty("onDeathLineKey").stringValue   = dto.onDeathLineKey ?? string.Empty;

            // Rivalen / Synergien
            SetStringList(sObj, "rivalCardIds", dto.rivalCardIds);
            SetStringList(sObj, "synergyCardIds", dto.synergyCardIds);

            // Ökosystem-Marker
            sObj.FindProperty("isEventCard").boolValue       = dto.isEventCard;
            sObj.FindProperty("isPremiumCard").boolValue     = dto.isPremiumCard;
            sObj.FindProperty("isPrestigeCard").boolValue    = dto.isPrestigeCard;
            sObj.FindProperty("isStarTempleCard").boolValue  = dto.isStarTempleCard;
            sObj.FindProperty("isSaisonPassCard").boolValue  = dto.isSaisonPassCard;

            sObj.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AbilityDefinition? ResolveAbility(string? id, IReadOnlyDictionary<string, AbilityDefinition> abilities)
            => string.IsNullOrEmpty(id) || !abilities.TryGetValue(id!, out var a) ? null : a;

        private static void SetStringList(SerializedObject sObj, string propName, List<string>? values)
        {
            var prop = sObj.FindProperty(propName);
            var list = values ?? new List<string>();
            prop.arraySize = list.Count;
            for (var i = 0; i < list.Count; i++)
                prop.GetArrayElementAtIndex(i).stringValue = list[i];
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
            sObj.FindProperty("recommendedCounterElement").enumValueIndex = (int)dto.recommendedCounterElement;
            sObj.FindProperty("recommendedPlayerLevel").intValue = dto.recommendedPlayerLevel;
            sObj.FindProperty("backgroundAddressableKey").stringValue = dto.backgroundAddressableKey ?? string.Empty;
            sObj.FindProperty("musicAddressableKey").stringValue = dto.musicAddressableKey ?? string.Empty;
            sObj.FindProperty("saeuleNameKey").stringValue = dto.saeuleNameKey ?? string.Empty;
            sObj.FindProperty("bossCardId").stringValue = dto.bossCardId ?? string.Empty;
            sObj.FindProperty("storySummaryKey").stringValue = dto.storySummaryKey ?? string.Empty;
            sObj.FindProperty("memoryFragmentKey").stringValue = dto.memoryFragmentKey ?? string.Empty;
            sObj.FindProperty("mentorNpcKey").stringValue = dto.mentorNpcKey ?? string.Empty;
            sObj.FindProperty("baseGoldPerDay").intValue = dto.baseGoldPerDay;
            sObj.FindProperty("prestige4CardId").stringValue = dto.prestige4CardId ?? string.Empty;

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
            public Race race = Race.Ritter;
            public int cost = 5;
            public int baseAttack = 100;
            public int baseHealth = 200;
            public int turnsToSpecial = 3;
            public string? baseAbilityId;
            public string? secondAbilityId;
            public string? thirdAbilityId;
            public string? lastWillAbilityId;
            public DeckLimit deckLimit = DeckLimit.Unlimited;
            public int globalCraftLimit = 90;
            public string? onPlayLineKey;
            public string? onVictoryLineKey;
            public string? onDeathLineKey;
            public List<string>? rivalCardIds;
            public List<string>? synergyCardIds;
            public bool isEventCard;
            public bool isPremiumCard;
            public bool isPrestigeCard;
            public bool isStarTempleCard;
            public bool isSaisonPassCard;
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
            public Race race = Race.Ritter;
            public string faehigkeitNameKey = string.Empty;
            public string faehigkeitDescKey = string.Empty;
            public HeroFaehigkeitsTyp faehigkeitsTyp = HeroFaehigkeitsTyp.KoeniglicheAura;
            public int magnitude;
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
            public Element recommendedCounterElement = Element.Feuer;
            public int recommendedPlayerLevel = 1;
            public string? backgroundAddressableKey;
            public string? musicAddressableKey;
            public string? saeuleNameKey;
            public string? bossCardId;
            public string? storySummaryKey;
            public string? memoryFragmentKey;
            public string? mentorNpcKey;
            public int baseGoldPerDay = 100;
            public string? prestige4CardId;
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

        // --- DTOs fuer die Querverweis-Validierung (nur die fuer die Pruefung relevanten Felder) ---

        [Serializable]
        private sealed class CollectionDto
        {
            public string id = string.Empty;
            public List<string>? requiredMaterialIds;
            public string? rewardCardId;
        }

        [Serializable]
        private sealed class FusionRecipeDto
        {
            public string? id;
            public string? resultCardId;
            public List<string>? requiredCardIds;
        }

        [Serializable]
        private sealed class MaterialDropDto
        {
            public string? nodeId;
            public List<MaterialDropEntryDto>? drops;
        }

        [Serializable]
        private sealed class MaterialDropEntryDto
        {
            public string? materialId;
        }

        [Serializable]
        private sealed class PremiumShopDto
        {
            public List<PremiumShopSlotDto>? permanentSlots;
            public List<PremiumShopSlotDto>? rotatingSlots;
        }

        [Serializable]
        private sealed class PremiumShopSlotDto
        {
            public string? cardId;
        }

        [Serializable]
        private sealed class StarTempleDto
        {
            public List<StarTempleExchangeDto>? exchanges;
        }

        [Serializable]
        private sealed class StarTempleExchangeDto
        {
            public string? id;
            public string? rewardCardId;
        }

        [Serializable]
        private sealed class EventDto
        {
            public string? id;
            public List<EventCardDto>? eventCards;
        }

        [Serializable]
        private sealed class EventCardDto
        {
            public string? cardId;
        }
    }
}
