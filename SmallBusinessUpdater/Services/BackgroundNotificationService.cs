using System;
using System.Threading.Tasks;
using SmallBusinessUpdater.Models;

namespace SmallBusinessUpdater.Services;

internal static class BackgroundNotificationService
{
    private static System.Windows.Forms.NotifyIcon? icon;
    private static System.Drawing.Icon? image;
    private static DateTimeOffset visibleUntil;

    public static void Show(AppConfig config, string title, string message, bool warning = false)
    {
        if (!config.ShowBackgroundNotifications) return;
        try
        {
            if (icon == null)
            {
                image = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
                icon = new System.Windows.Forms.NotifyIcon
                {
                    Icon = image ?? System.Drawing.SystemIcons.Information,
                    Text = "Small Business Updater",
                    Visible = true
                };
            }
            icon.ShowBalloonTip(6000, title, message,
                warning ? System.Windows.Forms.ToolTipIcon.Warning : System.Windows.Forms.ToolTipIcon.Info);
            visibleUntil = DateTimeOffset.UtcNow.AddSeconds(6);
        }
        catch (Exception ex)
        {
            LogService.Write("notifications.log", ex.ToString());
        }
    }

    public static async Task FlushAsync()
    {
        try
        {
            TimeSpan remaining = visibleUntil - DateTimeOffset.UtcNow;
            if (icon != null && remaining > TimeSpan.Zero)
                await Task.Delay(remaining);
        }
        finally { Dispose(); }
    }

    public static void Dispose()
    {
        icon?.Dispose();
        icon = null;
        image?.Dispose();
        image = null;
    }
}
