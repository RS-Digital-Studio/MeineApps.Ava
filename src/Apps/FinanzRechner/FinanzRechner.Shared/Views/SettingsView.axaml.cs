using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FinanzRechner.ViewModels;
using MeineApps.Core.Ava.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinanzRechner.Views;

public partial class SettingsView : UserControl
{
    private SettingsViewModel? _vm;

    public SettingsView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;

        _vm = vm;
        _vm.Initialize();

        // Events subscriben
        _vm.BackupCreated += OnBackupCreated;
        _vm.RestoreFileRequested += OnRestoreFileRequested;
        _vm.OpenUrlRequested += OnOpenUrlRequested;
        _vm.FeedbackRequested += OnFeedbackRequested;
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_vm == null) return;

        _vm.BackupCreated -= OnBackupCreated;
        _vm.RestoreFileRequested -= OnRestoreFileRequested;
        _vm.OpenUrlRequested -= OnOpenUrlRequested;
        _vm.FeedbackRequested -= OnFeedbackRequested;
        _vm = null;
    }

    /// <summary>
    /// Backup-Datei teilen/oeffnen via IFileShareService.
    /// </summary>
    private async void OnBackupCreated(string filePath)
    {
        try
        {
            var fileShareService = App.Services.GetRequiredService<IFileShareService>();
            await fileShareService.ShareFileAsync(filePath, "FinanzRechner Backup", "application/json");
        }
        catch (Exception)
        {
            // Fehler wird ignoriert - Backup-Datei bleibt im Temp-Verzeichnis
        }
    }

    /// <summary>
    /// Datei-Picker oeffnen fuer Restore.
    /// </summary>
    private async void OnRestoreFileRequested()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Restore Backup",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
            });

            if (files.Count == 0 || _vm == null)
            {
                // Abgebrochen - IsBackupInProgress zuruecksetzen
                if (_vm != null) _vm.IsBackupInProgress = false;
                return;
            }

            var file = files[0];
            var localPath = file.TryGetLocalPath();
            if (localPath != null && _vm != null)
            {
                // Merge/Replace-Dialog anzeigen
                _vm.OnRestoreFileSelected(localPath);
            }
            else if (_vm != null)
            {
                _vm.IsBackupInProgress = false;
            }
        }
        catch (Exception)
        {
            if (_vm != null) _vm.IsBackupInProgress = false;
        }
    }

    /// <summary>
    /// URL im Standardbrowser oeffnen.
    /// </summary>
    private void OnOpenUrlRequested(string url)
    {
        UriLauncher.OpenUri(url);
    }

    /// <summary>
    /// Feedback-E-Mail oeffnen.
    /// </summary>
    private void OnFeedbackRequested(string appName)
    {
        var uri = $"mailto:info@rs-digital.org?subject={Uri.EscapeDataString(appName + " Feedback")}";
        UriLauncher.OpenUri(uri);
    }
}
