using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace FinanzRechner.Services;

public sealed class FileDialogService : IFileDialogService
{
    public async Task<string?> SaveFileAsync(string suggestedFileName, string title, string filterName, params string[] extensions)
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return null;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            FileTypeChoices =
            [
                new FilePickerFileType(filterName) { Patterns = extensions.Select(e => $"*.{e}").ToList() }
            ]
        });

        return file?.Path.LocalPath;
    }

    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime single)
            return TopLevel.GetTopLevel(single.MainView);
        return null;
    }
}
