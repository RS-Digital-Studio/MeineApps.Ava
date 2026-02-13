using Android.Content;
using Android.Media;
using Android.Provider;
using FitnessRechner.Services;

namespace FitnessRechner.Android;

/// <summary>
/// Android-Implementierung für Erfolgs-Sounds.
/// Nutzt System-Notification-Sound (kein eigenes Asset nötig).
/// </summary>
public class AndroidFitnessSoundService : IFitnessSoundService
{
    private readonly Context _context;
    private MediaPlayer? _mediaPlayer;

    public bool IsEnabled { get; set; } = true;

    public AndroidFitnessSoundService(Context context)
    {
        _context = context;
    }

    public void PlaySuccess()
    {
        if (!IsEnabled) return;

        try
        {
            // Vorherigen Sound aufräumen
            _mediaPlayer?.Release();
            _mediaPlayer = null;

            // System-Notification-Sound als Erfolgs-Sound verwenden
            var uri = Settings.System.DefaultNotificationUri
                      ?? RingtoneManager.GetDefaultUri(RingtoneType.Notification);

            if (uri == null) return;

            _mediaPlayer = MediaPlayer.Create(_context, uri);
            if (_mediaPlayer == null) return;

            _mediaPlayer.SetVolume(0.5f, 0.5f); // Halbe Lautstärke
            _mediaPlayer.Completion += (_, _) =>
            {
                _mediaPlayer?.Release();
                _mediaPlayer = null;
            };
            _mediaPlayer.Start();
        }
        catch
        {
            // Sound ist optional - Fehler ignorieren
            _mediaPlayer?.Release();
            _mediaPlayer = null;
        }
    }
}
