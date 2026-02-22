using Avalonia.Media;

namespace BomberBlast.Models;

/// <summary>
/// Basis-Kategorie fuer Skin-Items im Shop
/// </summary>
public enum SkinCategory
{
    Player,
    Bomb,
    Explosion,
    Trail,
    Victory,
    Frame
}

/// <summary>
/// Darstellungs-Modell für einen Skin in der Shop-Ansicht.
/// Wird für Spieler-, Bomben- und Explosions-Skins verwendet.
/// </summary>
public class SkinDisplayItem
{
    /// <summary>Skin-ID (z.B. "gold", "bomb_fire")</summary>
    public string Id { get; init; } = "";

    /// <summary>Kategorie (Player, Bomb, Explosion)</summary>
    public SkinCategory Category { get; init; }

    /// <summary>Lokalisierter Anzeigename</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>Primärfarbe des Skins</summary>
    public Color PrimaryColor { get; init; }

    /// <summary>Sekundärfarbe (Akzent)</summary>
    public Color SecondaryColor { get; init; }

    /// <summary>Ob nur für Premium-Nutzer</summary>
    public bool IsPremiumOnly { get; init; }

    /// <summary>Ob dieser Skin gerade ausgewählt ist</summary>
    public bool IsEquipped { get; set; }

    /// <summary>Ob gesperrt (Premium-Only + kein Premium ODER nicht gekauft)</summary>
    public bool IsLocked { get; set; }

    /// <summary>Ob der Skin einen Glow-Effekt hat</summary>
    public bool HasGlow { get; init; }

    /// <summary>Coin-Preis (0 = kostenlos/Standard)</summary>
    public int CoinPrice { get; init; }

    /// <summary>Ob bereits gekauft/freigeschaltet</summary>
    public bool IsOwned { get; set; }

    /// <summary>Status-Text ("Ausgewählt", "Nur Premium" etc.)</summary>
    public string StatusText { get; set; } = "";

    /// <summary>Karten-Opacity (gedimmt wenn gesperrt)</summary>
    public double CardOpacity => IsLocked ? 0.5 : 1.0;

    /// <summary>Ob der Auswählen-Button sichtbar ist (owned + nicht equipped)</summary>
    public bool CanSelect => IsOwned && !IsLocked && !IsEquipped;

    /// <summary>Ob der Kauf-Button sichtbar ist (nicht owned, nicht premium-locked)</summary>
    public bool CanBuy => !IsOwned && !IsLocked && CoinPrice > 0;
}
