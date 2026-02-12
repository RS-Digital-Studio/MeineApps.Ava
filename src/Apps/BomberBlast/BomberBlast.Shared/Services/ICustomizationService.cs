using BomberBlast.Models;

namespace BomberBlast.Services;

/// <summary>
/// Service für Spieler-/Gegner-Skins und visuelle Anpassungen
/// </summary>
public interface ICustomizationService
{
    /// <summary>Aktuell gewählter Spieler-Skin</summary>
    SkinDefinition PlayerSkin { get; }

    /// <summary>Aktuell gewähltes Gegner-Skin-Set</summary>
    string EnemySkinSet { get; }

    /// <summary>Alle verfügbaren Spieler-Skins</summary>
    IReadOnlyList<SkinDefinition> AvailablePlayerSkins { get; }

    /// <summary>Spieler-Skin setzen</summary>
    void SetPlayerSkin(string skinId);

    /// <summary>Gegner-Skin-Set setzen</summary>
    void SetEnemySkinSet(string setId);
}
