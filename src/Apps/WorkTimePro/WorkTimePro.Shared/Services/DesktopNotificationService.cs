using System.Collections.Concurrent;
using System.Diagnostics;

namespace WorkTimePro.Services;

/// <summary>
/// Desktop Notification Service (Windows Toast / Linux notify-send)
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

            // Single-quoted Here-String (@'...'@) verhindert PowerShell-Injection ($() / ` / $env)
            var script = $@"
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null
$template = @'
<toast>
    <visual>
        <binding template=""ToastGeneric"">
            <text>{safeTitle}</text>
            <text>{safeBody}</text>
        </binding>
    </visual>
    <audio src=""ms-winsoundevent:Notification.Default""/>
</toast>
'@
$xml = New-Object Windows.Data.Xml.Dom.XmlDocument
$xml.LoadXml($template)
$toast = [Windows.UI.Notifications.ToastNotification]::new($xml)
[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('WorkTimePro').Show($toast)";

            // EncodedCommand umgeht alle Escaping-Probleme (Base64-kodiertes UTF-16 Script)
            var encodedScript = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
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
