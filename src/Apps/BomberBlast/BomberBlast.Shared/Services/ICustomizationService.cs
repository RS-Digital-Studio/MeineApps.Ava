using BomberBlast.Models;
using BomberBlast.Models.Cosmetics;

namespace BomberBlast.Services;

/// <summary>
/// Service für Spieler-/Gegner-/Bomben-/Explosions-Skins, Trails, Sieges-Animationen und Profilrahmen
/// </summary>
public interface ICustomizationService
{
    /// <summary>Aktuell gewählter Spieler-Skin</summary>
    SkinDefinition PlayerSkin { get; }

    /// <summary>Aktuell gewähltes Gegner-Skin-Set</summary>
    string EnemySkinSet { get; }

    /// <summary>Aktuell gewählter Bomben-Skin</summary>
    BombSkinDefinition BombSkin { get; }

    /// <summary>Aktuell gewählter Explosions-Skin</summary>
    ExplosionSkinDefinition ExplosionSkin { get; }

    /// <summary>Alle verfügbaren Spieler-Skins</summary>
    IReadOnlyList<SkinDefinition> AvailablePlayerSkins { get; }

    /// <summary>Alle verfügbaren Bomben-Skins</summary>
    IReadOnlyList<BombSkinDefinition> AvailableBombSkins { get; }

    /// <summary>Alle verfügbaren Explosions-Skins</summary>
    IReadOnlyList<ExplosionSkinDefinition> AvailableExplosionSkins { get; }

    /// <summary>Spieler-Skin setzen</summary>
    void SetPlayerSkin(string skinId);

    /// <summary>Prüft ob ein Spieler-Skin gekauft/freigeschaltet ist</summary>
    bool IsPlayerSkinOwned(string skinId);

    /// <summary>Spieler-Skin kaufen</summary>
    bool TryPurchasePlayerSkin(string skinId);

    /// <summary>Gegner-Skin-Set setzen</summary>
    void SetEnemySkinSet(string setId);

    /// <summary>Bomben-Skin setzen</summary>
    void SetBombSkin(string skinId);

    /// <summary>Explosions-Skin setzen</summary>
    void SetExplosionSkin(string skinId);

    /// <summary>Prüft ob ein Bomben-Skin gekauft/freigeschaltet ist</summary>
    bool IsBombSkinOwned(string skinId);

    /// <summary>Prüft ob ein Explosions-Skin gekauft/freigeschaltet ist</summary>
    bool IsExplosionSkinOwned(string skinId);

    /// <summary>Bomben-Skin kaufen</summary>
    bool TryPurchaseBombSkin(string skinId);

    /// <summary>Explosions-Skin kaufen</summary>
    bool TryPurchaseExplosionSkin(string skinId);

    // === Trails ===

    /// <summary>Aktuell gewählter Trail (null = kein Trail)</summary>
    TrailDefinition? ActiveTrail { get; }

    /// <summary>Alle verfügbaren Trail-Definitionen</summary>
    IReadOnlyList<TrailDefinition> AvailableTrails { get; }

    /// <summary>Trail setzen (null/leer = deaktivieren)</summary>
    void SetTrail(string? trailId);

    /// <summary>Prüft ob ein Trail gekauft ist</summary>
    bool IsTrailOwned(string trailId);

    /// <summary>Trail kaufen (Coins)</summary>
    bool TryPurchaseTrail(string trailId);

    // === Sieges-Animationen ===

    /// <summary>Aktuell gewählte Sieges-Animation (null = Standard)</summary>
    VictoryDefinition? ActiveVictory { get; }

    /// <summary>Alle verfügbaren Sieges-Animationen</summary>
    IReadOnlyList<VictoryDefinition> AvailableVictories { get; }

    /// <summary>Sieges-Animation setzen (null/leer = Standard)</summary>
    void SetVictory(string? victoryId);

    /// <summary>Prüft ob eine Sieges-Animation gekauft ist</summary>
    bool IsVictoryOwned(string victoryId);

    /// <summary>Sieges-Animation kaufen (Coins)</summary>
    bool TryPurchaseVictory(string victoryId);

    // === Profilrahmen ===

    /// <summary>Aktuell gewählter Profilrahmen (null = kein Rahmen)</summary>
    FrameDefinition? ActiveFrame { get; }

    /// <summary>Alle verfügbaren Profilrahmen</summary>
    IReadOnlyList<FrameDefinition> AvailableFrames { get; }

    /// <summary>Profilrahmen setzen (null/leer = deaktivieren)</summary>
    void SetFrame(string? frameId);

    /// <summary>Prüft ob ein Profilrahmen gekauft ist</summary>
    bool IsFrameOwned(string frameId);

    /// <summary>Profilrahmen kaufen (Coins)</summary>
    bool TryPurchaseFrame(string frameId);
}
