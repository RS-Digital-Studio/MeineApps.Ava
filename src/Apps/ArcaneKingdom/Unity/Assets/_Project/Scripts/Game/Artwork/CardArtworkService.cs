#nullable enable
using System.Collections.Generic;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Cards;
using Cysharp.Threading.Tasks;
using UnityEngine;
#if ARCANE_ADDRESSABLES_INSTALLED
using UnityEngine.AddressableAssets;
#endif

namespace ArcaneKingdom.Game.Artwork
{
    /// <summary>
    /// Liefert Sprites fuer Karten. Ladestrategie:
    ///   1. Wenn <see cref="CardDefinition.ArtworkAddressableKey"/> gesetzt: per
    ///      Addressables laden (sofern Addressables-Symbol gesetzt ist)
    ///   2. Sonst (oder bei Fehler): procedural generierter Element-Gradient
    ///
    /// Cache-Strategie: Procedural-Sprites werden EINMALIG pro (Element, Rarity)
    /// generiert und wiederverwendet — minimaler Speicher-Footprint.
    /// </summary>
    public sealed class CardArtworkService
    {
        private readonly Dictionary<string, Sprite> _addressableCache = new();
        private readonly Dictionary<(Element, Rarity), Sprite> _proceduralCache = new();

        /// <summary>
        /// Liefert das Sprite (oder ein procedural-generiertes Fallback) fuer eine Karte.
        /// Asynchron weil Addressables-Load IO ist.
        /// </summary>
        public async UniTask<Sprite> GetSpriteAsync(CardDefinition card)
        {
            // 1. Addressable-Lookup (wenn Key + Package vorhanden)
            if (!string.IsNullOrEmpty(card.ArtworkAddressableKey))
            {
                if (_addressableCache.TryGetValue(card.ArtworkAddressableKey, out var cached))
                    return cached;

#if ARCANE_ADDRESSABLES_INSTALLED
                try
                {
                    var handle = Addressables.LoadAssetAsync<Sprite>(card.ArtworkAddressableKey);
                    var sprite = await handle.Task.AsUniTask();
                    if (sprite != null)
                    {
                        _addressableCache[card.ArtworkAddressableKey] = sprite;
                        return sprite;
                    }
                }
                catch (System.Exception ex)
                {
                    GameLogger.Warning("Artwork",
                        $"Addressable '{card.ArtworkAddressableKey}' nicht geladen: {ex.Message}");
                }
#endif
            }

            // 2. Procedural-Fallback (immer gecached pro Element/Rarity)
            var key = (card.Element, card.Rarity);
            if (!_proceduralCache.TryGetValue(key, out var procedural))
            {
                procedural = GenerateProcedural(card.Element, card.Rarity);
                _proceduralCache[key] = procedural;
            }
            return procedural;
        }

        // ============================================================
        // Procedural Sprite Generator
        // ============================================================

        private static Sprite GenerateProcedural(Element element, Rarity rarity)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[size * size];
            var top = ElementColorTop(element);
            var bottom = ElementColorBottom(element);
            var rarityTint = RarityTint(rarity);

            for (var y = 0; y < size; y++)
            {
                var t = y / (float)(size - 1);
                var gradient = Color.Lerp(bottom, top, t);
                gradient = Color.Lerp(gradient, rarityTint, 0.15f);

                for (var x = 0; x < size; x++)
                {
                    // Sanftes Vignette (dunkler an den Raendern)
                    var dx = (x - size * 0.5f) / (size * 0.5f);
                    var dy = (y - size * 0.5f) / (size * 0.5f);
                    var dist = Mathf.Sqrt(dx * dx + dy * dy);
                    var vignette = Mathf.Clamp01(1f - dist * 0.5f);
                    var c = gradient * vignette;
                    c.a = 1f;
                    pixels[y * size + x] = c;
                }
            }

            // Schmaler Highlight-Streifen oben (Element-Akzent)
            for (var y = size - 6; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var alpha = (y - (size - 6)) / 6f;
                pixels[y * size + x] = Color.Lerp(pixels[y * size + x], Color.white, alpha * 0.35f);
            }

            tex.SetPixels(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Color ElementColorTop(Element e) => e switch
        {
            Element.Feuer  => new Color(1.00f, 0.45f, 0.20f),
            Element.Wasser => new Color(0.30f, 0.55f, 0.95f),
            Element.Licht  => new Color(1.00f, 0.92f, 0.55f),
            Element.Dunkel => new Color(0.55f, 0.30f, 0.65f),
            _              => new Color(0.45f, 0.85f, 0.45f)  // Natur
        };

        private static Color ElementColorBottom(Element e) => e switch
        {
            Element.Feuer  => new Color(0.32f, 0.10f, 0.05f),
            Element.Wasser => new Color(0.05f, 0.15f, 0.32f),
            Element.Licht  => new Color(0.35f, 0.28f, 0.10f),
            Element.Dunkel => new Color(0.18f, 0.08f, 0.22f),
            _              => new Color(0.10f, 0.30f, 0.10f)
        };

        private static Color RarityTint(Rarity r) => r switch
        {
            Rarity.Ungewoehnlich => new Color(0.40f, 0.78f, 0.40f),
            Rarity.Selten        => new Color(0.40f, 0.65f, 0.95f),
            Rarity.Epic          => new Color(0.70f, 0.45f, 0.95f),
            Rarity.Legendaer     => new Color(0.95f, 0.78f, 0.30f),
            _                    => Color.gray
        };
    }
}
