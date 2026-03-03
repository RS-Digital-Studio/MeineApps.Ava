using Avalonia.Controls;
using Avalonia.Controls.Templates;
using MeineApps.Core.Ava.ViewModels;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace MeineApps.Core.Ava;

public class ViewLocator : IDataTemplate
{
    private static readonly ConcurrentDictionary<Type, Type?> ViewTypeCache = new();

    public Control? Build(object? data)
    {
        if (data is null) return null;

        var vmType = data.GetType();
        var viewType = ViewTypeCache.GetOrAdd(vmType, ResolveViewType);

        if (viewType is not null)
            return (Control)Activator.CreateInstance(viewType)!;

        return new TextBlock { Text = $"View nicht gefunden: {vmType.Name}" };
    }

    public bool Match(object? data) => data is ViewModelBase;

    private static Type? ResolveViewType(Type vmType)
    {
        var fullName = vmType.FullName;
        if (fullName is null) return null;

        // Konvention: .ViewModels. → .Views. | ViewModel → View
        var viewName = fullName
            .Replace(".ViewModels.", ".Views.")
            .Replace("ViewModel", "View");

        // Primär: Gleiches Assembly (Shared-Projekt)
        var viewType = vmType.Assembly.GetType(viewName);

#if DEBUG
        if (viewType is null)
            Debug.WriteLine($"ViewLocator: Keine View gefunden fuer {fullName} -> {viewName}");
#endif

        return viewType;
    }
}
