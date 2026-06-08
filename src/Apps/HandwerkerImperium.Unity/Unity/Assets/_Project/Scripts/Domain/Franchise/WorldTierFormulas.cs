#nullable enable
using System;

namespace HandwerkerImperium.Domain.Franchise
{
    /// <summary>
    /// Franchise-Karte / World-Tiers (P2 §3, GDD §5): 4 Städte (Hansstadt → Kreisstadt → Großstadt → Metropole),
    /// 3 Prestige-Übergänge. Jede Stadt ist größer — höhere Stern-Schwellen + skaliertes Income-Ziel —, sodass die
    /// Akte trotz Prestige-Multiplikator länger werden (PROGRESSION §2). Reine, Unity-freie Skalierungs-Mathematik.
    /// </summary>
    public static class WorldTierFormulas
    {
        /// <summary>Anzahl Städte (= MaxPrestige + 1).</summary>
        public const int CityCount = 4;

        /// <summary>Stadt-Index auf [0, CityCount-1] klemmen.</summary>
        public static int ClampCityIndex(int cityIndex)
        {
            if (cityIndex < 0) return 0;
            return cityIndex >= CityCount ? CityCount - 1 : cityIndex;
        }

        /// <summary>
        /// Stern-Schwellen-Skalierung der Stadt: Schwellen steigen je Stadt um den Faktor
        /// <c>perCityScale^cityIndex</c> (Akte werden länger, nicht kürzer).
        /// </summary>
        public static double StarThresholdScale(int cityIndex, double perCityScale)
        {
            cityIndex = ClampCityIndex(cityIndex);
            return Math.Pow(perCityScale, cityIndex);
        }

        /// <summary>
        /// Income-Ziel-Skalierung der Stadt — wächst stärker als der Prestige-Multiplikator, damit die größere
        /// Stadt netto neue Spielzeit erzeugt (<c>perCityScale^cityIndex</c>).
        /// </summary>
        public static decimal CityIncomeTargetScale(int cityIndex, decimal perCityScale)
        {
            cityIndex = ClampCityIndex(cityIndex);
            decimal scale = 1m;
            for (int i = 0; i < cityIndex; i++)
                scale *= perCityScale;
            return scale;
        }

        /// <summary>True, wenn dies die Endstadt (Metropole) ist — danach kein Prestige mehr, Endgame-Loop.</summary>
        public static bool IsFinalCity(int cityIndex) => ClampCityIndex(cityIndex) == CityCount - 1;
    }
}
