using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace BingXBot;

/// <summary>
/// Avalonia 12 Android Application-Bootstrap. Avalonia initialisiert sich hier
/// EINMAL pro Prozess — nicht mehr pro Activity. Activities wie <see cref="MainActivity"/>
/// werden mit der bereits laufenden App-Instanz verbunden.
/// </summary>
[Application]
public class AndroidApp : AvaloniaAndroidApplication<App>
{
    protected AndroidApp(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // Inter-Font fuer einheitliches UI ueber alle Plattformen.
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
