namespace ImeLocker.UI;

using System.Drawing;
using System.Windows.Forms;

public sealed class TrayIconManager
{
    private readonly NotifyIcon _notifyIcon;

    public TrayIconManager(NotifyIcon notifyIcon)
    {
        _notifyIcon = notifyIcon;
    }

    /// <summary>Update tooltip to show current window and group info.</summary>
    public void UpdateStatus(string processName, string? groupName)
    {
        var text = groupName is not null
            ? $"ImeLocker - {processName} [{groupName}]"
            : $"ImeLocker - {processName} [默认]";
        // NotifyIcon.Text max 63 chars
        _notifyIcon.Text = text.Length > 63 ? text[..63] : text;
    }

    public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(3000, title, text, icon);
    }
}
