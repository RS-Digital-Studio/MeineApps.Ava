using BomberBlast.Models.Entities;

namespace BomberBlast.Models.Cards;

/// <summary>
/// Eine vom Spieler besessene Karte mit Level und Duplikat-Zähler.
/// Wird als Teil der CardCollection in JSON persistiert.
/// </summary>
public class OwnedCard
{
    /// <summary>Welcher Bombentyp</summary>
    public BombType BombType { get; set; }

    /// <summary>Anzahl der gesammelten Karten (für Upgrades: Duplikate)</summary>
    public int Count { get; set; } = 1;

    /// <summary>Kartenlevel: 1=Bronze, 2=Silber, 3=Gold</summary>
    public int Level { get; set; } = 1;

    /// <summary>Maximales Kartenlevel</summary>
    public const int MaxLevel = 3;

    /// <summary>Ob die Karte noch upgegraded werden kann</summary>
    public bool CanUpgrade => Level < MaxLevel;
}

/// <summary>
/// Eine im Deck ausgerüstete Karte während des Gameplays.
/// Trackt verbleibende Einsätze für das aktuelle Level.
/// </summary>
public class EquippedCard
{
    /// <summary>Bombentyp dieser Karte</summary>
    public BombType BombType { get; set; }

    /// <summary>Verbleibende Einsätze in diesem Level</summary>
    public int RemainingUses { get; set; }

    /// <summary>Kartenlevel (für Stärke-Skalierung: 1=Bronze +0%, 2=Silber +20%, 3=Gold +40%)</summary>
    public int CardLevel { get; set; } = 1;

    /// <summary>Rarität (für HUD-Rendering)</summary>
    public Rarity Rarity { get; set; }

    /// <summary>Ob diese Karte noch einsetzbar ist</summary>
    public bool HasUsesLeft => RemainingUses > 0;

    /// <summary>Stärke-Multiplikator basierend auf Kartenlevel</summary>
    public float StrengthMultiplier => CardLevel switch
    {
        2 => 1.2f,  // Silber: +20%
        3 => 1.4f,  // Gold: +40%
        _ => 1.0f   // Bronze: Basis
    };
}
