using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace HandwerkerImperium.Android;

/// <summary>
/// Avalonia 12 Android Application-Bootstrap. Avalonia initialisiert sich hier EINMAL pro Prozess.
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
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
