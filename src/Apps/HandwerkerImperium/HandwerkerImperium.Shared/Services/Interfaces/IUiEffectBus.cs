using System;
using HandwerkerImperium.Graphics;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Zentraler Bus für UI-Effekt-Anfragen: FloatingText, Confetti-Celebration und
/// Full-Screen-Reward-Zeremonie. Entkoppelt die Effekt-Auslöser (MainViewModel,
/// Coordinators, Feature-VMs) von den Effekt-Sinks (DashboardView, MainView).
///
/// Lifecycle: Singleton im DI. Die Views abonnieren den Bus direkt im Code-Behind
/// (analog <c>IFrameClock</c>), die Auslöser injizieren ihn und rufen die Raise-Methoden.
/// </summary>
public interface IUiEffectBus
{
    /// <summary>FloatingText-Anfrage mit Kategorie (money/xp/level/golden_screws/warning/...).</summary>
    event Action<string, string>? FloatingTextRequested;

    /// <summary>Confetti-Overlay-Anfrage (Level-Up, Achievement, Prestige, Meilensteine).</summary>
    event Action? CelebrationRequested;

    /// <summary>Full-Screen-Reward-Zeremonie-Anfrage (grosse Meilensteine).</summary>
    event Action<CeremonyType, string, string>? CeremonyRequested;

    /// <summary>Löst einen FloatingText aus.</summary>
    void RaiseFloatingText(string text, string category);

    /// <summary>Löst ein Confetti-Celebration-Overlay aus.</summary>
    void RaiseCelebration();

    /// <summary>Löst eine Full-Screen-Reward-Zeremonie aus.</summary>
    void RaiseCeremony(CeremonyType type, string title, string subtitle);
}
