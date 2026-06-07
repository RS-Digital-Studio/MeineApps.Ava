namespace HandwerkerImperium.Domain.Guild
{
    // Wert-Enums der Gilden-Netzwerk-DTOs (CoopOrder/Auction/MegaProject). Reine Werte —
    // die zugehörigen Firebase-DTOs selbst bleiben für die Netzwerk-Schicht.
    // 1:1-Port aus dem Avalonia-Original (Models/Firebase/*). Enum-Reihenfolge = Persistenz-Integer.

    /// <summary>Status eines Co-op-Auftrags zwischen zwei Gildenmitgliedern.</summary>
    public enum CoopOrderStatus
    {
        /// <summary>Eingeladener Spieler hat noch nicht geantwortet (5min Timeout).</summary>
        Pending,
        /// <summary>Beide Spieler aktiv im MiniGame.</summary>
        Active,
        /// <summary>Beide haben fertig — Reward verteilt.</summary>
        Completed,
        /// <summary>Pending-Timeout abgelaufen oder einer hat abgelehnt.</summary>
        Expired
    }

    /// <summary>Status einer Worker-Markt-Auktion.</summary>
    public enum WorkerAuctionStatus
    {
        /// <summary>30s Vorwarnung — Bid-Phase startet bald.</summary>
        Warming,
        /// <summary>Aktive 30s-Bid-Phase.</summary>
        Active,
        /// <summary>Höchstbieter erhält Worker, Verlierer bekommen Geld zurück.</summary>
        Settled
    }

    /// <summary>Mega-Projekt-Template-Typ.</summary>
    public enum GuildMegaProjectType
    {
        /// <summary>Gilden-Kathedrale — mittlere Schwierigkeit, ~2 Wochen.</summary>
        Cathedral = 0,
        /// <summary>Gilden-Hauptquartier — End-Game-Ziel, ~4 Wochen.</summary>
        Headquarters = 1
    }
}
