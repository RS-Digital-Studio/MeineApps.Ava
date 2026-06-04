using System.Resources;

namespace SunSeeker.Shared.Resources.Strings;

/// <summary>
/// Zugriff auf den eingebetteten Ressourcen-Satz (AppStrings.*.resx). Nur der ResourceManager
/// wird gebraucht — die Strings werden ueber <c>ILocalizationService.GetString(key)</c> bzw. die
/// <c>{loc:Translate}</c>-Markup-Extension geholt, daher keine generierten Einzel-Properties.
/// </summary>
internal static class AppStrings
{
    private static ResourceManager? _resourceManager;

    internal static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager(
            "SunSeeker.Shared.Resources.Strings.AppStrings", typeof(AppStrings).Assembly);
}
