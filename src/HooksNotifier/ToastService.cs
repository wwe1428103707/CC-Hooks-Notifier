using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace HooksNotifier;

internal static class ToastService
{
    private const string Aumid = "ClaudeCode.HooksNotifier";

    /// <summary>
    /// Show a Windows toast notification. Falls back to balloon if WinRT fails.
    /// </summary>
    public static void Show(string title, string body)
    {
        try
        {
            var template = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
            var textNodes = template.GetElementsByTagName("text");

            textNodes[0].AppendChild(template.CreateTextNode(title));
            textNodes[1].AppendChild(template.CreateTextNode(body));

            var toast = new ToastNotification(template);
            ToastNotificationManager.CreateToastNotifier(Aumid).Show(toast);
        }
        catch (Exception ex)
        {
            try { ShowBalloon(title, body); }
            catch { Log.Error($"toast failed (WinRT: {ex.Message}, fallback also failed)"); }
        }
    }

    /// <summary>
    /// Fallback: Windows Forms balloon notification.
    /// </summary>
    public static void ShowBalloon(string title, string body)
    {
        using var icon = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            Visible = true,
            BalloonTipTitle = title,
            BalloonTipText = body
        };
        icon.ShowBalloonTip(5000);
        Thread.Sleep(600);
        icon.Visible = false;
    }
}
