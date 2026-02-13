using System.Collections.Concurrent;
using System.Diagnostics;

namespace ZeitManager.Services;

/// <summary>
/// Desktop notification service using OS-native notifications
/// </summary>
public class DesktopNotificationService : INotificationService
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _scheduledNotifications = [];

    public Task ShowNotificationAsync(string title, string body, string? actionId = null)
    {
        if (OperatingSystem.IsWindows())
        {
            ShowWindowsNotification(title, body);
        }
        else if (OperatingSystem.IsLinux())
        {
            ShowLinuxNotification(title, body);
        }

        return Task.CompletedTask;
    }

    public Task ScheduleNotificationAsync(string id, string title, string body, DateTime triggerAt)
    {
        CancelNotificationSync(id);

        var delay = triggerAt.ToUniversalTime() - DateTime.UtcNow;
        if (delay <= TimeSpan.Zero)
        {
            return ShowNotificationAsync(title, body, id);
        }

        var cts = new CancellationTokenSource();
        _scheduledNotifications[id] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cts.Token);
                if (!cts.Token.IsCancellationRequested)
                {
                    await ShowNotificationAsync(title, body, id);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                _scheduledNotifications.TryRemove(id, out _);
                cts.Dispose();
            }
        }, cts.Token);

        return Task.CompletedTask;
    }

    public bool CanScheduleExactAlarms() => true;

    public Task CancelNotificationAsync(string id)
    {
        CancelNotificationSync(id);
        return Task.CompletedTask;
    }

    private void CancelNotificationSync(string id)
    {
        if (_scheduledNotifications.TryRemove(id, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    private static void ShowWindowsNotification(string title, string body)
    {
        try
        {
            var safeTitle = EscapeXml(title);
            var safeBody = EscapeXml(body);

            // Use PowerShell to show a Windows Toast Notification
            var script = $@"
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null
$template = @""
<toast>
    <visual>
        <binding template=""ToastGeneric"">
            <text>{safeTitle}</text>
            <text>{safeBody}</text>
        </binding>
    </visual>
    <audio src=""ms-winsoundevent:Notification.Default""/>
</toast>
""@
$xml = New-Object Windows.Data.Xml.Dom.XmlDocument
$xml.LoadXml($template)
$toast = [Windows.UI.Notifications.ToastNotification]::new($xml)
[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('ZeitManager').Show($toast)";

            // Base64-encodiertes Script verhindert Escaping-Probleme mit Anführungszeichen
            var scriptBytes = System.Text.Encoding.Unicode.GetBytes(script);
            var encodedScript = Convert.ToBase64String(scriptBytes);

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encodedScript}",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch
        {
            // Fallback: Console beep
            Console.Write('\a');
        }
    }

    private static void ShowLinuxNotification(string title, string body)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "notify-send",
                Arguments = $"\"{EscapeShell(title)}\" \"{EscapeShell(body)}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch
        {
            Console.Write('\a');
        }
    }

    private static string EscapeXml(string text)
    {
        return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                   .Replace("\"", "&quot;").Replace("'", "&apos;");
    }

    private static string EscapeShell(string text)
    {
        return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("$", "\\$").Replace("`", "\\`");
    }
}
