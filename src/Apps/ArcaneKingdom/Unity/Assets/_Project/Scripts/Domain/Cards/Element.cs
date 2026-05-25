namespace ArcaneKingdom.Domain.Cards
{
    /// <summary>
    /// Die sechs Elemente aus dem Doppel-Dreieck-System (Designplan v4 Kap. 3).
    /// Physisches Dreieck: Feuer → Natur → Wasser → Feuer.
    /// Magisches Dreieck:  Licht → Dunkel → Erde → Licht.
    /// Effektivitätsmatrix in <see cref="ArcaneKingdom.Domain.Battle.ElementMatchup"/>.
    /// </summary>
    public enum Element
    {
        /// <summary>Feuer — stark gegen Natur, schwach gegen Wasser. Verbrennung, Schild-Pierce 15%.</summary>
        Feuer = 0,

        /// <summary>Wasser — stark gegen Feuer, schwach gegen Natur. Einfrierungs-Chance.</summary>
        Wasser = 1,

        /// <summary>Natur — stark gegen Wasser, schwach gegen Feuer. Selbstheilung über Zeit.</summary>
        Natur = 2,

        /// <summary>Erde — stark gegen Licht, schwach gegen Dunkel. Steinpanzer, Erdbeben, Verwurzelung.</summary>
        Erde = 3,

        /// <summary>Dunkel — stark gegen Erde, schwach gegen Licht. Gift/Fluch-Verstärkung.</summary>
        Dunkel = 4,

        /// <summary>Licht — stark gegen Dunkel, schwach gegen Erde. Hebt Status-Effekte auf.</summary>
        Licht = 5
    }
}
