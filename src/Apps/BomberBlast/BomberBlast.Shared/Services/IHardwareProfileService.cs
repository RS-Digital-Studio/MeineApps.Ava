namespace BomberBlast.Services;

/// <summary>
/// Hardware-Quality-Tier (Phase 27 — AAA-Audit P2).
/// Skaliert visuelle Effekte je nach Geräte-Klasse. Auto-Detection via Geräte-RAM-Heuristik
/// (Android), explicit-Override per Settings.
/// </summary>
public enum HardwareTier
{
    /// <summary>Low-End (≤ 2 GB RAM, alte CPUs). Particle-Cap 300, kein Bloom, ReducedEffects on.</summary>
    Low = 0,
    /// <summary>Mid-Tier (3–4 GB RAM). Particle-Cap 800, normale Effekte, kein Bloom.</summary>
    Medium = 1,
    /// <summary>High-Tier (5–8 GB RAM). Particle-Cap 1500, alle Effekte aktiv.</summary>
    High = 2,
    /// <summary>Ultra (>8 GB RAM, Flagships). Particle-Cap 1500 + Bloom-Pass aktiv.</summary>
    Ultra = 3,
}

/// <summary>
/// Hardware-Profil-Service (Phase 27 — AAA-Audit P2/P3/P4).
///
/// <para>Zentralisiert die Quality-Tier-Logik plus Battery-Profile (Akku-Sparen) und
/// Thermal-Throttle-Status (wenn Android Thermal API verfügbar). Wird vom Renderer
/// abgefragt um Particle-Cap, Shader-Komplexität und Haptic-Frequenz anzupassen.</para>
///
/// <para>Auto-Tier-Detection-Strategie auf Android: GC.GetTotalMemory + Environment.ProcessorCount.
/// User kann Override in Settings setzen (Persistenz: <c>HardwareTierOverride</c>).</para>
/// </summary>
public interface IHardwareProfileService
{
    /// <summary>Aktueller effektiver Tier (Auto-Detection ODER User-Override).</summary>
    HardwareTier CurrentTier { get; }

    /// <summary>Auto-detektierter Tier (nur lesbar — User-Override schlägt das in CurrentTier).</summary>
    HardwareTier DetectedTier { get; }

    /// <summary>True wenn der User explizit einen Override gesetzt hat.</summary>
    bool HasUserOverride { get; }

    /// <summary>
    /// Setzt einen User-Override. <c>null</c> = Auto-Detection wieder aktiv.
    /// </summary>
    void SetUserOverride(HardwareTier? tier);

    /// <summary>
    /// Liefert die maximal erlaubte Anzahl Particles für den aktuellen Tier.
    /// Phase 21 hat Cap auf 1500 erhöht — Low-Tier rollt auf 300 zurück.
    /// </summary>
    int GetMaxParticles();

    /// <summary>True wenn Bloom-Pass auf diesem Tier aktiv sein soll (nur Ultra).</summary>
    bool ShouldEnableBloom();

    /// <summary>
    /// Battery-Profile: True wenn Battery-Save-Modus aktiv (User-Toggle in Settings).
    /// Bei aktivem Battery-Save reduziert der Renderer Particle, deaktiviert Haptic-Patterns
    /// und drosselt Background-Asset-Loading.
    /// </summary>
    bool BatterySaveActive { get; set; }

    /// <summary>
    /// Thermal-Throttle-Status (Android Thermal API). Bei aktivem Thermal-Throttle
    /// werden teure Effekte (Bloom, Slow-Motion) ausgesetzt.
    /// </summary>
    bool ThermalThrottleActive { get; set; }

    /// <summary>Wird gefeuert wenn sich der effektive Quality-State ändert.</summary>
    event Action? QualityChanged;

    // === Phase 27b — Memory + Network State Hooks ============================

    /// <summary>
    /// Phase 27b — Wird vom Plattform-Layer aufgerufen wenn das OS Memory-Druck signalisiert
    /// (Android: <c>onTrimMemory</c>). Service kann Bitmap-Caches releasen, Particles drosseln.
    /// </summary>
    /// <param name="trimLevel">Android-TrimLevel (5/10/15/20/40/60/80 — höher = aggressiver).</param>
    void OnMemoryTrimRequested(int trimLevel);

    /// <summary>
    /// True wenn das letzte OnMemoryTrimRequested mit hohem TrimLevel aufgerufen wurde.
    /// Renderer kann Cache-Cap reduzieren, GameAssetService kann Evict-Aggressive aktivieren.
    /// </summary>
    bool MemoryPressureActive { get; }

    /// <summary>
    /// Network-State-Hook (online/offline). Wird vom Plattform-Layer aktualisiert.
    /// CloudSaveService liest das vor jedem Push: Wenn offline → defer.
    /// </summary>
    bool IsNetworkAvailable { get; set; }

    /// <summary>Event wenn sich der Netzwerkstatus ändert (für CloudSaveService-Subscription).</summary>
    event Action<bool>? NetworkStateChanged;
}
