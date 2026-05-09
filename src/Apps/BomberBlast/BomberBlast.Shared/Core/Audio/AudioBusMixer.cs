using MeineApps.Core.Ava.Services;

namespace BomberBlast.Core.Audio;

/// <summary>
/// Verwaltet die Bus-Volumes pro <see cref="AudioBus"/> + Side-Chain-Ducking.
/// Persistiert via IPreferencesService (Bus_Vol_{Name}, Default je nach Bus).
///
/// <para>Ducking-Modell (Sidechain-Compressor-Light):</para>
/// Wenn ein wichtiger Bus aktiv ist (z.B. Voice oder Cinematic), werden andere Buses
/// (typisch Music + Ambient) für eine begrenzte Dauer auf einen Duck-Faktor abgesenkt.
/// Die Duck-Hüllkurve klingt linear ab und wird via <see cref="Update(float)"/> getickt.
/// </summary>
public sealed class AudioBusMixer
{
    private const int BusCount = 7;

    private readonly IPreferencesService _prefs;
    private readonly float[] _busVolumes = new float[BusCount];
    private readonly float[] _duckMultipliers = new float[BusCount];
    private readonly float[] _duckTargets = new float[BusCount];
    private readonly float[] _duckTimers = new float[BusCount];
    private readonly float[] _duckDurations = new float[BusCount];

    /// <summary>
    /// Wird gefeuert wenn sich der effektive Volume eines Buses ändert.
    /// Receiver können darauf reagieren um z.B. die laufende Musik live nachzuregeln.
    /// </summary>
    public event Action<AudioBus, float>? BusVolumeChanged;

    public AudioBusMixer(IPreferencesService preferences)
    {
        _prefs = preferences;
        for (int i = 0; i < BusCount; i++)
        {
            _busVolumes[i] = (float)_prefs.Get($"Bus_Vol_{(AudioBus)i}", DefaultVolumeFor((AudioBus)i));
            _duckMultipliers[i] = 1f;
            _duckTargets[i] = 1f;
        }
    }

    /// <summary>Default-Volumes je Bus (Studio-Konvention: Music tendenziell unter SFX).</summary>
    private static double DefaultVolumeFor(AudioBus bus) => bus switch
    {
        AudioBus.Master => 1.0,
        AudioBus.Music => 0.7,
        AudioBus.Ambient => 0.5,
        AudioBus.Sfx => 1.0,
        AudioBus.Ui => 0.85,
        AudioBus.Voice => 1.0,
        AudioBus.Cinematic => 1.0,
        _ => 1.0,
    };

    /// <summary>Aktuelles statisches Bus-Volume (ohne Duck-Modulation).</summary>
    public float GetBusVolume(AudioBus bus) => _busVolumes[(int)bus];

    /// <summary>
    /// Setzt das Bus-Volume und persistiert. Feuert <see cref="BusVolumeChanged"/>.
    /// </summary>
    public void SetBusVolume(AudioBus bus, float volume)
    {
        var clamped = Math.Clamp(volume, 0f, 1f);
        var idx = (int)bus;
        if (Math.Abs(_busVolumes[idx] - clamped) < 0.001f) return;

        _busVolumes[idx] = clamped;
        _prefs.Set($"Bus_Vol_{bus}", (double)clamped);
        BusVolumeChanged?.Invoke(bus, GetEffectiveVolume(bus, 1f));
    }

    /// <summary>
    /// Liefert den effektiven Volume-Multiplikator für eine PlaySound-Aufruf.
    /// Berechnung: <c>busVol * masterVol * duckMul * playVol</c>.
    /// Master-Bus selbst wird NICHT zusätzlich mit sich selbst multipliziert.
    /// </summary>
    public float GetEffectiveVolume(AudioBus bus, float playVolume)
    {
        var idx = (int)bus;
        var master = bus == AudioBus.Master ? 1f : _busVolumes[(int)AudioBus.Master];
        return _busVolumes[idx] * master * _duckMultipliers[idx] * playVolume;
    }

    /// <summary>
    /// Triggert ein Sidechain-Duck auf einen oder mehrere Ziel-Buses.
    /// </summary>
    /// <param name="targetBus">Welcher Bus geduckt werden soll (z.B. Music).</param>
    /// <param name="duckMultiplier">Faktor 0..1 (0.3 = 30% während Duck).</param>
    /// <param name="durationSeconds">Dauer der Duck-Phase. Danach Linear-Recovery (0.5s).</param>
    public void Duck(AudioBus targetBus, float duckMultiplier, float durationSeconds)
    {
        var idx = (int)targetBus;
        var clamp = Math.Clamp(duckMultiplier, 0f, 1f);
        // Stärkster aktiver Duck gewinnt — verhindert dass kurze Voice-Line einen längeren Cinematic-Duck überschreibt.
        if (clamp < _duckTargets[idx])
            _duckTargets[idx] = clamp;
        if (durationSeconds > _duckDurations[idx])
        {
            _duckDurations[idx] = durationSeconds;
            _duckTimers[idx] = durationSeconds;
        }
    }

    /// <summary>
    /// Triggert ein Standard-Music-Ducking (Music + Ambient auf 30% für 1.5s).
    /// Wird typisch bei Voice-Lines oder Cinematic-Stingern verwendet.
    /// </summary>
    public void DuckForCinematic(float durationSeconds = 1.5f, float multiplier = 0.3f)
    {
        Duck(AudioBus.Music, multiplier, durationSeconds);
        Duck(AudioBus.Ambient, multiplier, durationSeconds);
    }

    /// <summary>
    /// Pro-Frame-Update: löst Duck-Hüllkurven auf.
    /// Während Duck-Phase ist Multiplier auf Target. Nach Ablauf: Linear-Recovery zu 1.0.
    /// </summary>
    public void Update(float deltaTime)
    {
        for (int i = 0; i < BusCount; i++)
        {
            if (_duckTimers[i] > 0f)
            {
                _duckTimers[i] -= deltaTime;
                _duckMultipliers[i] = _duckTargets[i];
                if (_duckTimers[i] <= 0f)
                {
                    // Recovery starten — 0.5s Linear zu 1.0
                    _duckTimers[i] = 0f;
                    _duckDurations[i] = 0f;
                }
            }
            else if (_duckMultipliers[i] < 1f)
            {
                // Linear-Recovery (0.5s)
                _duckMultipliers[i] = Math.Min(1f, _duckMultipliers[i] + deltaTime * 2f);
                if (_duckMultipliers[i] >= 1f)
                    _duckTargets[i] = 1f;
            }
        }
    }
}
