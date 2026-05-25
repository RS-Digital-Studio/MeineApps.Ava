#nullable enable
using System.Collections.Generic;
using ArcaneKingdom.Domain.Cards;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArcaneKingdom.Game.Artwork
{
    /// <summary>
    /// Zentraler Lade-Service für alle UI-Assets jenseits der Karten-Artworks:
    /// Avatare, Helden-Portraits, Welt-Hintergründe, Element-/Rassen-Icons,
    /// Currency-Icons, Rune-Icons, Avatar-Rahmen, Pack-Designs, Achievement-/Quest-Icons,
    /// Saison-Pass-Belohnungen, Node-Marker, NPCs, UI-Backgrounds, Brand-Logo.
    ///
    /// Ladestrategie: Resources.Load&lt;Texture2D&gt; + Sprite.Create zur Laufzeit
    /// (analog zu CardArtworkService). Lazy Cache pro Asset-ID/Schlüssel.
    /// </summary>
    public sealed class UIAssetService
    {
        private readonly Dictionary<string, Sprite?> _cache = new();
        private readonly Dictionary<string, Texture2D?> _texCache = new();

        public Sprite? GetAvatar(string avatarId)            => Load($"Avatars/{avatarId}");
        public Sprite? GetHeroPortrait(Race race)            => Load($"Heroes/portrait_{RaceFolder(race)}");
        public Sprite? GetWorldBackground(string worldId)    => Load($"Worlds/{worldId}");
        public Sprite? GetBattleBackground(string worldId)   => Load($"Battle/Backgrounds/battle_bg_{worldId}");
        public Sprite? GetElementIcon(Element element)       => Load($"Icons/Elements/{ElementFolder(element)}");
        public Sprite? GetRaceEmblem(Race race)              => Load($"Icons/Races/{RaceFolder(race)}");
        public Sprite? GetCurrency(string id)                => Load($"Icons/Currency/{id}");
        public Sprite? GetRune(string runeId)                => Load($"Runes/{runeId}");
        public Sprite? GetFrame(string frameId)              => Load($"Icons/Frames/{frameId}");
        public Sprite? GetPack(string packId)                => Load($"Packs/{packId}");
        public Sprite? GetAchievement(string achievementId)  => Load($"Achievements/{achievementId}");
        public Sprite? GetQuest(string questId)              => Load($"Quests/{questId}");
        public Sprite? GetSaisonReward(string rewardId)      => Load($"SaisonPass/{rewardId}");
        public Sprite? GetNodeMarker(string nodeType)        => Load($"Icons/Nodes/node_{nodeType.ToLowerInvariant()}");
        public Sprite? GetNpc(string npcId)                  => Load($"NPCs/{npcId}");
        public Sprite? GetUIBackground(string uiId)          => Load($"UI/{uiId}");
        public Sprite? GetBrandLogo()                        => Load("Brand/logo_arcanekingdom");
        public Sprite? GetStarSprite()                       => Load("UI/star_sprite");
        public Sprite? GetCardBack(string rarityId)          => Load($"Cards/Backs/cardback_{rarityId.ToLowerInvariant()}");
        public Sprite? GetEffect(string effectId)            => Load($"Effects/effect_{effectId}");
        public Sprite? GetPath(string pathState)             => Load($"Paths/path_{pathState.ToLowerInvariant()}");

        /// <summary>
        /// Cache leeren — z.B. bei Scene-Wechseln, wenn nicht-genutzte Sprites
        /// freigegeben werden sollen.
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
            _texCache.Clear();
        }

        // === UI Toolkit Convenience: Background-Image direkt auf VisualElements setzen ===

        /// <summary>
        /// Setzt das Background-Image eines VisualElements basierend auf einem Resource-Pfad.
        /// Fallback (Asset fehlt) belaesst das Element unveraendert.
        /// </summary>
        public void ApplyBackground(VisualElement? element, string resourcePath, ScaleMode scaleMode = ScaleMode.ScaleAndCrop)
        {
            if (element == null) return;
            var tex = LoadTexture(resourcePath);
            if (tex == null) return;
            element.style.backgroundImage = new StyleBackground(tex);
            element.style.unityBackgroundScaleMode = new StyleEnum<ScaleMode>(scaleMode);
        }

        /// <summary>Welt-Background direkt aufs VisualElement.</summary>
        public void ApplyWorldBackground(VisualElement? element, string worldId)
            => ApplyBackground(element, $"Worlds/{worldId}");

        /// <summary>Battle-Background direkt aufs VisualElement (16:9 pro Welt).</summary>
        public void ApplyBattleBackground(VisualElement? element, string worldId)
            => ApplyBackground(element, $"Battle/Backgrounds/battle_bg_{worldId}");

        /// <summary>UI-Background direkt aufs VisualElement (z.B. hub_main, login, splash, arena, gilde, tempel, zauberschmiede).</summary>
        public void ApplyUIBackground(VisualElement? element, string uiId)
            => ApplyBackground(element, $"UI/{uiId}");

        /// <summary>Brand-Logo direkt aufs VisualElement (ScaleMode.ScaleToFit, behaelt Aspect-Ratio).</summary>
        public void ApplyBrandLogo(VisualElement? element)
            => ApplyBackground(element, "Brand/logo_arcanekingdom", ScaleMode.ScaleToFit);

        /// <summary>Avatar-Sprite direkt aufs VisualElement (rund, ScaleMode.ScaleAndCrop).</summary>
        public void ApplyAvatar(VisualElement? element, string avatarId)
            => ApplyBackground(element, $"Avatars/{avatarId}", ScaleMode.ScaleAndCrop);

        /// <summary>Helden-Portrait pro Rasse aufs VisualElement.</summary>
        public void ApplyHeroPortrait(VisualElement? element, Race race)
            => ApplyBackground(element, $"Heroes/portrait_{RaceFolder(race)}", ScaleMode.ScaleAndCrop);

        /// <summary>Currency-Icon (gold/diamant/energie/scrap_*) aufs VisualElement.</summary>
        public void ApplyCurrencyIcon(VisualElement? element, string id)
            => ApplyBackground(element, $"Icons/Currency/{id}", ScaleMode.ScaleToFit);

        /// <summary>Element-Wappen aufs VisualElement.</summary>
        public void ApplyElementIcon(VisualElement? element, Element element_)
            => ApplyBackground(element, $"Icons/Elements/{ElementFolder(element_)}", ScaleMode.ScaleToFit);

        /// <summary>Rassen-Emblem aufs VisualElement.</summary>
        public void ApplyRaceEmblem(VisualElement? element, Race race)
            => ApplyBackground(element, $"Icons/Races/{RaceFolder(race)}", ScaleMode.ScaleToFit);

        /// <summary>NPC-Portrait aufs VisualElement.</summary>
        public void ApplyNpc(VisualElement? element, string npcId)
            => ApplyBackground(element, $"NPCs/{npcId}", ScaleMode.ScaleAndCrop);

        /// <summary>Node-Marker auf WorldMap.</summary>
        public void ApplyNodeMarker(VisualElement? element, string nodeType)
            => ApplyBackground(element, $"Icons/Nodes/node_{nodeType.ToLowerInvariant()}", ScaleMode.ScaleToFit);

        /// <summary>
        /// Achievement-Icon aus Resources/Achievements/. Sucht zuerst spezifisches Asset
        /// (z.B. arena_top_10.png), fallt zurueck auf generisches Bronze-Icon wenn ID
        /// nicht matched (z.B. wenn JSON-Definitions neue Achievements einfuegt).
        /// </summary>
        public void ApplyAchievement(VisualElement? element, string achievementId)
        {
            if (element == null) return;
            var specific = LoadTexture($"Achievements/{achievementId}");
            var tex = specific ?? LoadTexture("Quests/achievement_bronze");
            if (tex == null) return;
            element.style.backgroundImage = new StyleBackground(tex);
            element.style.unityBackgroundScaleMode = new StyleEnum<ScaleMode>(ScaleMode.ScaleToFit);
        }

        // === Interne Helfer (Texture-Loader fuer UI Toolkit) ===

        private Texture2D? LoadTexture(string resourcePath)
        {
            if (_texCache.TryGetValue(resourcePath, out var cached))
                return cached;
            var tex = Resources.Load<Texture2D>(resourcePath);
            _texCache[resourcePath] = tex;
            return tex;
        }

        // === Interne Helfer ===

        private Sprite? Load(string resourcePath)
        {
            if (_cache.TryGetValue(resourcePath, out var cached))
                return cached;
            var tex = Resources.Load<Texture2D>(resourcePath);
            Sprite? sprite = null;
            if (tex != null)
            {
                sprite = Sprite.Create(
                    tex,
                    new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit: 100f,
                    extrude: 0,
                    meshType: SpriteMeshType.FullRect);
                sprite.name = resourcePath;
            }
            _cache[resourcePath] = sprite;
            return sprite;
        }

        private static string RaceFolder(Race race) => race switch
        {
            Race.Goetter     => "goetter",
            Race.Elfen       => "elfen",
            Race.Tiergeister => "tiergeister",
            Race.Daemonen    => "daemonen",
            _                => "ritter"
        };

        private static string ElementFolder(Element element) => element switch
        {
            Element.Feuer  => "feuer",
            Element.Wasser => "wasser",
            Element.Erde   => "erde",
            Element.Licht  => "licht",
            Element.Dunkel => "dunkel",
            _              => "natur"
        };
    }
}
