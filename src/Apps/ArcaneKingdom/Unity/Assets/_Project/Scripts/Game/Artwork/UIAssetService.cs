#nullable enable
using System.Collections.Generic;
using ArcaneKingdom.Domain.Cards;
using UnityEngine;

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
        public void ClearCache() => _cache.Clear();

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
