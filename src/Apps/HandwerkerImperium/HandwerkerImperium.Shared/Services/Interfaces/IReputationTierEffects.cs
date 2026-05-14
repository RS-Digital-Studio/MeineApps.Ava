using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Events;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// (MainViewModel-Zerlegung): Bündelt die Effekt-Logik beim Reputation-Tier-Up
/// (FloatingText + Celebration + Audio-Stinger + Achievement-Dialog mit Tier-Effekten).
///
/// MainViewModel ruft <see cref="HandleTierChanged"/> auf seinem UI-Thread-Handler auf —
/// die Property-Notify-Aufrufe (CurrentReputationTier, ReputationTierName, ...) bleiben im
/// MainViewModel, weil sie an MVVM-PropertyChanged gebunden sind.
/// </summary>
public interface IReputationTierEffects
{
    /// <summary>
    /// Verarbeitet einen Tier-Change. Bei Aufstieg ueber Beginner werden FloatingText,
    /// Celebration, Audio und ggf. ein Achievement-Dialog ausgeloest.
    /// </summary>
    /// <param name="e">Event-Args mit OldTier/NewTier/IsUp.</param>
    /// <param name="floatingTextRaiser">Delegate fuer FloatingText-Trigger (text, kind).</param>
    /// <param name="celebrationRaiser">Delegate fuer CelebrationOverlay-Trigger.</param>
    /// <param name="achievementDialog">Optionaler Achievement-Dialog-Setter (name, desc).</param>
    void HandleTierChanged(
        ReputationTierChangedEventArgs e,
        Action<string, string> floatingTextRaiser,
        Action celebrationRaiser,
        Action<string, string>? achievementDialog);
}
