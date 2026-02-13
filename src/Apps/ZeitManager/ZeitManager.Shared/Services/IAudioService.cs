using ZeitManager.Models;

namespace ZeitManager.Services;

public interface IAudioService
{
    /// <summary>Eingebaute + System-Sounds kombiniert.</summary>
    IReadOnlyList<SoundItem> AvailableSounds { get; }

    /// <summary>System-Ringtones (Android) bzw. leere Liste (Desktop).</summary>
    IReadOnlyList<SoundItem> SystemSounds { get; }

    Task PlayAsync(string soundId, bool loop = false);

    /// <summary>Spielt einen Sound per URI (content:// oder Dateipfad) ab.</summary>
    Task PlayUriAsync(string uri, bool loop = false);

    void Stop();
    string DefaultTimerSound { get; }
    string DefaultAlarmSound { get; }

    /// <summary>Öffnet einen System-Sound-Picker (RingtoneManager auf Android, FilePicker auf Desktop).</summary>
    Task<SoundItem?> PickSoundAsync();
}
