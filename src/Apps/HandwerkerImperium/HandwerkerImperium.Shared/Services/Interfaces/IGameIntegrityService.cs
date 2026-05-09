using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// HMAC-SHA256-Signierung von Gilden-relevanten Spielwerten.
/// Erkennt Manipulationen am SaveGame bevor Daten an Firebase gesendet werden.
/// </summary>
public interface IGameIntegrityService
{
    /// <summary>
    /// Berechnet die HMAC-SHA256-Signatur ueber Gilden-relevante Werte
    /// und speichert sie in <see cref="GameState.IntegritySignature"/>.
    /// </summary>
    void ComputeSignature(GameState state);

    /// <summary>
    /// Prueft ob die gespeicherte Signatur mit den aktuellen Werten uebereinstimmt.
    /// Gibt false zurueck wenn keine Signatur vorhanden oder manipuliert.
    /// </summary>
    bool VerifySignature(GameState state);

    /// <summary>
    /// v2.1.0: Generischer HMAC-SHA256 ueber einen beliebigen String. Wird von
    /// Co-op-/Auktions-Services genutzt, um Score- und Bid-Daten manipulationssicher
    /// in Firebase abzulegen.
    /// </summary>
    string ComputeStringHmac(string payload);
}
