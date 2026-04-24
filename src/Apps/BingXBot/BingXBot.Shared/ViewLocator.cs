using Avalonia.Controls;
using Avalonia.Controls.Templates;
using MeineApps.Core.Ava.ViewModels;

namespace BingXBot;

/// <summary>
/// Löst ViewModel → View per Namens-Konvention auf:
/// <c>BingXBot.ViewModels.DashboardViewModel</c> → <c>BingXBot.Views.DashboardView</c>.
///
/// Auf Mobile-Shell (Android) wird zuerst die <c>XyzViewMobile</c>-Variante probiert;
/// falls nicht vorhanden, fällt der Locator auf die Desktop-View zurück.
///
/// Wird in App.axaml als <c>&lt;Application.DataTemplates&gt;</c> registriert — dadurch rendert
/// jedes ContentControl/ItemsControl mit einem <see cref="ViewModelBase"/> als Content
/// automatisch die richtige View, inklusive korrektem DataContext.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    public Control Build(object? param)
    {
        if (param is null) return CreateError("ViewLocator: null ViewModel");

        var vmType = param.GetType();
        var vmName = vmType.FullName;
        if (string.IsNullOrEmpty(vmName))
            return CreateError($"ViewLocator: Kein FullName für {vmType}");

        // Konvention: ViewModels-Namespace → Views-Namespace, "ViewModel"-Suffix → "View".
        var baseViewName = vmName
            .Replace(".ViewModels.", ".Views.")
            .Replace("ViewModel", "View");

        // Mobile-Shell probiert zuerst die "Mobile"-Variante.
        if (App.IsMobileShell)
        {
            var mobileType = ResolveType(baseViewName + "Mobile", vmType);
            if (mobileType != null)
                return Instantiate(mobileType);
        }

        var type = ResolveType(baseViewName, vmType);
        if (type == null)
            return CreateError($"ViewLocator: View nicht gefunden für {vmName}");

        return Instantiate(type);
    }

    public bool Match(object? data) => data is ViewModelBase;

    /// <summary>
    /// Sucht den Typ zuerst in der Assembly des ViewModels, dann via Type.GetType.
    /// Das vermeidet "ViewLocator findet nichts weil Assembly nicht geladen ist"-Probleme.
    /// </summary>
    private static Type? ResolveType(string fullName, Type vmType)
    {
        // 1. Gleiche Assembly wie ViewModel (häufigster Fall)
        var type = vmType.Assembly.GetType(fullName);
        if (type != null) return type;

        // 2. Assembly-Qualified Lookup über alle geladenen Assemblies
        type = Type.GetType(fullName);
        if (type != null) return type;

        // 3. Suche in allen geladenen Assemblies (Fallback)
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(fullName);
            if (type != null) return type;
        }
        return null;
    }

    private static Control Instantiate(Type viewType)
    {
        if (Activator.CreateInstance(viewType) is Control control) return control;
        return CreateError($"ViewLocator: {viewType.Name} ist kein Control");
    }

    private static Control CreateError(string message) =>
        new TextBlock { Text = message, Foreground = Avalonia.Media.Brushes.Red, Margin = new Avalonia.Thickness(16) };
}
