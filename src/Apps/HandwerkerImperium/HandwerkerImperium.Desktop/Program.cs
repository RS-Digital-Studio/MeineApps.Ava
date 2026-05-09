using Avalonia;
using HandwerkerImperium.Desktop;
using HandwerkerImperium.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace HandwerkerImperium;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Plattformspezifischer Audio-Service (NAudio auf Windows, ffplay-Fallback auf Linux/macOS).
        // AAA-Audit P2: Desktop war bisher Stub, ist jetzt voll funktional.
        App.AudioServiceFactory = sp =>
            new DesktopAudioService(sp.GetRequiredService<IGameStateService>());

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
