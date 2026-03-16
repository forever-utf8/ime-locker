namespace ImeLocker.UI;

using System.Drawing;
using System.Windows.Forms;

public sealed class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private Icon? _icon;

    public TrayIconManager(NotifyIcon notifyIcon)
    {
        _notifyIcon = notifyIcon;
        RegenerateIcon();
    }

    /// <summary>Update tooltip to reflect current window.</summary>
    public void UpdateTooltip(string processName, string? groupName)
    {
        var text = groupName is not null
            ? $"ImeLocker - {processName} [{groupName}]"
            : $"ImeLocker - {processName} [默认]";
        _notifyIcon.Text = text.Length > 63 ? text[..63] : text;
    }

    public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(3000, title, text, icon);
    }

    /// <summary>Regenerate icon (e.g. after theme change).</summary>
    public void RegenerateIcon()
    {
        var old = _icon;
        _icon = TrayIconGenerator.CreateIcon();
        _notifyIcon.Icon = _icon;
        old?.Dispose();
    }

    public void Dispose()
    {
        _icon?.Dispose();
    }
}
