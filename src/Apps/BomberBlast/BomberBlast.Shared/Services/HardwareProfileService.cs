using BomberBlast.Core.Diagnostics;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Default-Implementation für <see cref="IHardwareProfileService"/> (Phase 27 — AAA-Audit P2/P3/P4).
///
/// <para>Auto-Detection-Heuristik: ProcessorCount + GC.GetTotalMemory(false). Funktioniert auf
/// Android via Mono.Runtime und auf Desktop. Default-Tier ist Medium wenn Detection fehlschlägt.</para>
///
/// <para>Persistenz: <c>HardwareTierOverride</c> (int, -1 = no override) in Preferences.</para>
/// </summary>
public sealed class HardwareProfileService : IHardwareProfileService
{
    private const string OverrideKey = "HardwareTierOverride";
    private const string BatterySaveKey = "BatterySaveActive";

    private readonly IPreferencesService _prefs;
    private readonly HardwareTier _detected;
    private HardwareTier? _userOverride;
    private bool _batterySave;
    private bool _thermalThrottle;
    // Phase 27b
    private bool _memoryPressure;
    private bool _networkAvailable = true;
    private DateTime _memoryPressureExpiresUtc = DateTime.MinValue;

    public event Action? QualityChanged;
    public event Action<bool>? NetworkStateChanged;

    public HardwareProfileService(IPreferencesService prefs)
    {
        _prefs = prefs;
        _detected = DetectTier();

        // User-Override laden (-1 = none)
        var overrideRaw = _prefs.Get(OverrideKey, -1);
        _userOverride = overrideRaw >= 0 && overrideRaw <= (int)HardwareTier.Ultra
            ? (HardwareTier?)overrideRaw
            : null;

        _batterySave = _prefs.Get(BatterySaveKey, false);
    }

    public HardwareTier DetectedTier => _detected;
    public HardwareTier CurrentTier => _userOverride ?? _detected;
    public bool HasUserOverride => _userOverride.HasValue;

    public void SetUserOverride(HardwareTier? tier)
    {
        _userOverride = tier;
        _prefs.Set(OverrideKey, tier.HasValue ? (int)tier.Value : -1);
        QualityChanged?.Invoke();
    }

    public int GetMaxParticles()
    {
        var tier = CurrentTier;
        // Battery-Save / Thermal-Throttle / Memory-Pressure drücken den effektiven Tier um eine Stufe.
        // Memory-Pressure zählt nochmal extra (kann auf Low fallen wenn alles aktiv).
        if (_batterySave || _thermalThrottle || MemoryPressureActive)
        {
            tier = (HardwareTier)Math.Max(0, (int)tier - 1);
        }
        if (MemoryPressureActive && tier > HardwareTier.Low)
        {
            tier = (HardwareTier)Math.Max(0, (int)tier - 1);
        }
        return tier switch
        {
            HardwareTier.Low => 300,
            HardwareTier.Medium => 800,
            HardwareTier.High => 1500,
            HardwareTier.Ultra => 1500,
            _ => 800,
        };
    }

    public bool ShouldEnableBloom()
    {
        if (_batterySave || _thermalThrottle || MemoryPressureActive) return false;
        return CurrentTier == HardwareTier.Ultra;
    }

    public bool BatterySaveActive
    {
        get => _batterySave;
        set
        {
            if (_batterySave == value) return;
            _batterySave = value;
            _prefs.Set(BatterySaveKey, value);
            QualityChanged?.Invoke();
        }
    }

    public bool ThermalThrottleActive
    {
        get => _thermalThrottle;
        set
        {
            if (_thermalThrottle == value) return;
            _thermalThrottle = value;
            // Thermal-State NICHT persistieren (transient, vom OS getrieben)
            GameEngineEventSource.Log.HardwareTierChanged((int)CurrentTier, _batterySave, _thermalThrottle);
            QualityChanged?.Invoke();
        }
    }

    // === Phase 27b — Memory + Network Hooks =================================

    /// <summary>
    /// Memory-Pressure aktiv wenn TrimLevel im letzten OnMemoryTrimRequested >= 40 (Android-Threshold)
    /// UND der Trigger weniger als 60s zurückliegt (transient, OS gibt evtl. Memory wieder frei).
    /// </summary>
    public bool MemoryPressureActive => _memoryPressure && DateTime.UtcNow < _memoryPressureExpiresUtc;

    public void OnMemoryTrimRequested(int trimLevel)
    {
        // Android-TrimLevels: 5/10/15 = leicht, 20/40 = mittel, 60/80 = kritisch.
        // Erst ab Level 40 reduzieren wir Quality.
        var heapBytes = GC.GetTotalMemory(forceFullCollection: false);
        GameEngineEventSource.Log.MemoryTrimRequested(trimLevel, heapBytes);

        if (trimLevel >= 40)
        {
            _memoryPressure = true;
            // Pressure läuft nach 60s ab — falls kein neuer Trim kommt
            _memoryPressureExpiresUtc = DateTime.UtcNow.AddSeconds(60);
            QualityChanged?.Invoke();
        }
    }

    public bool IsNetworkAvailable
    {
        get => _networkAvailable;
        set
        {
            if (_networkAvailable == value) return;
            _networkAvailable = value;
            GameEngineEventSource.Log.NetworkStateChanged(value);
            NetworkStateChanged?.Invoke(value);
        }
    }

    /// <summary>
    /// Heuristik für Hardware-Tier. ProcessorCount + GC-Memory-Nutzung.
    /// Werte sind defensiv (lieber Mid statt High, wenn unsicher).
    /// </summary>
    private static HardwareTier DetectTier()
    {
        try
        {
            int cores = Math.Max(1, Environment.ProcessorCount);
            // GC-Memory ist nur ein grober Indikator (eigener Heap-Footprint, nicht Geräte-RAM).
            // Aber: Wenn der eigene Heap nach Init schon > 80 MB ist, hat das Gerät genug RAM.
            long heapMb = GC.GetTotalMemory(false) / (1024 * 1024);

            if (cores >= 8 && heapMb >= 60) return HardwareTier.Ultra;
            if (cores >= 6 && heapMb >= 40) return HardwareTier.High;
            if (cores >= 4 && heapMb >= 25) return HardwareTier.Medium;
            return HardwareTier.Low;
        }
        catch
        {
            return HardwareTier.Medium;
        }
    }
}
