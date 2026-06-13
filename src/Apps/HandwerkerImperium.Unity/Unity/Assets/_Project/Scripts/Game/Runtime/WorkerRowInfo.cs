namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Schreibgeschützter Status einer Werkstatt-Zeile im Worker-Verwaltungs-Panel (GDD §6.2):
    /// genug Information, damit das HUD eine Zeile ohne Domain-Zugriff rendern kann.
    /// </summary>
    public struct WorkerRowInfo
    {
        public bool Unlocked;
        public bool HasWorker;
        public int Level;
        public int MaxLevel;
        public decimal HireCost;
        public decimal UpgradeCost;
        public bool AtMax;
    }
}
