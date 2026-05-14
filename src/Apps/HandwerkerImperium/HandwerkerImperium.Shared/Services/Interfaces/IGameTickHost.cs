using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Host-Facade für <see cref="GameTickCoordinator"/>. Per-Tick-Orchestrierung berührt
/// inhärent breiten UI-State — dieser Host bündelt die MainViewModel-Zugriffe, die der
/// Tick-Code noch braucht (ActivePage-Gating, EconomyVM-Refreshes, Event-Display).
/// </summary>
public interface IGameTickHost
{
    /// <summary>Aktuelle Seite — für das tab-spezifische Refresh-Gating.</summary>
    ActivePage ActivePage { get; }

    /// <summary>True wenn das Worker-Profil-Overlay sichtbar ist (kein ActivePage-Wert).</summary>
    bool IsWorkerProfileActive { get; }

    /// <summary>True wenn der Feierabend-Rush aktiv ist.</summary>
    bool IsRushActive { get; }

    /// <summary>True wenn der Rush gestartet werden kann.</summary>
    bool CanActivateRush { get; }

    /// <summary>True wenn der Boost-Indikator (Rush/SpeedBoost) sichtbar ist.</summary>
    bool ShowBoostIndicator { get; }

    /// <summary>True wenn gerade ein Spiel-Event aktiv ist.</summary>
    bool HasActiveEvent { get; }

    /// <summary>Aktualisiert die Netto-Einkommen-Anzeige im Header.</summary>
    void UpdateNetIncomeHeader(GameState state);

    /// <summary>Aktualisiert die Rush-Timer-Anzeige.</summary>
    void UpdateRushDisplay();

    /// <summary>Aktualisiert den Boost-Indikator.</summary>
    void UpdateBoostIndicator();

    /// <summary>Aktualisiert die Lieferant-Anzeige.</summary>
    void UpdateDeliveryDisplay();

    /// <summary>Aktualisiert Event-Anzeige + saisonalen Modifikator.</summary>
    void UpdateEventDisplay();

    /// <summary>Aktualisiert nur den Event-Timer-Text.</summary>
    void UpdateEventTimer();

    /// <summary>Aktualisiert die Reputations-Anzeige.</summary>
    void RefreshReputation(GameState state);

    /// <summary>Aktualisiert das Prestige-Banner.</summary>
    void RefreshPrestigeBanner(GameState state);

    /// <summary>Aktualisiert die Worker-Warnung im Dashboard-Banner.</summary>
    void UpdateWorkerWarning(GameState state);
}
