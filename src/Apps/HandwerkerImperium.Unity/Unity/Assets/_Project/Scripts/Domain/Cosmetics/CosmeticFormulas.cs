#nullable enable
using System.Collections.Generic;

namespace HandwerkerImperium.Domain.Cosmetics
{
    /// <summary>Art eines Cosmetics (GDD §9.4).</summary>
    public enum CosmeticKind { AvatarSkin = 0, WorkshopSkin = 1, DecoTheme = 2 }

    /// <summary>Bezahlwährung eines Cosmetics.</summary>
    public enum CosmeticCurrency { Money = 0, Gems = 1, EventCurrency = 2 }

    /// <summary>Cosmetic-Definition: Art, Preis, Währung (datengetrieben, Katalog im Game-Layer).</summary>
    public sealed class CosmeticDefinition
    {
        public string Id;
        public CosmeticKind Kind;
        public CosmeticCurrency Currency;
        public decimal Price;

        public CosmeticDefinition(string id, CosmeticKind kind, CosmeticCurrency currency, decimal price)
        {
            Id = id;
            Kind = kind;
            Currency = currency;
            Price = price;
        }
    }

    /// <summary>Ergebnis eines Kaufversuchs.</summary>
    public enum CosmeticPurchaseResult { Success = 0, AlreadyOwned = 1, NotEnoughCurrency = 2, Invalid = 3 }

    /// <summary>
    /// Cosmetics (GDD §9.4): Avatar-/Werkstatt-Skins + Stadt-Deko-Themes — niedrigschwellige, druckfreie
    /// Monetarisierung mit hohem Sichtbarkeitswert. Reine, Unity-freie Besitz-/Kauf-Logik; Besitz + aktiver
    /// Skin liegen im Save-Slice, der Katalog als Content im Game-Layer.
    /// </summary>
    public static class CosmeticFormulas
    {
        /// <summary>True, wenn das Cosmetic bereits besessen wird.</summary>
        public static bool IsOwned(IReadOnlyCollection<string>? owned, string id)
        {
            if (owned == null || string.IsNullOrEmpty(id)) return false;
            foreach (var x in owned)
                if (x == id) return true;
            return false;
        }

        /// <summary>True, wenn das Guthaben für den Preis reicht.</summary>
        public static bool CanAfford(decimal balance, decimal price) => balance >= price;

        /// <summary>
        /// Prüft einen Kauf (ohne Seiteneffekt): bereits besessen / zu wenig Guthaben / Erfolg. Der Game-Layer
        /// zieht bei <see cref="CosmeticPurchaseResult.Success"/> das Guthaben ab und ergänzt den Besitz.
        /// </summary>
        public static CosmeticPurchaseResult EvaluatePurchase(CosmeticDefinition? def, decimal balance, IReadOnlyCollection<string>? owned)
        {
            if (def == null || string.IsNullOrEmpty(def.Id)) return CosmeticPurchaseResult.Invalid;
            if (IsOwned(owned, def.Id)) return CosmeticPurchaseResult.AlreadyOwned;
            if (!CanAfford(balance, def.Price)) return CosmeticPurchaseResult.NotEnoughCurrency;
            return CosmeticPurchaseResult.Success;
        }
    }
}
