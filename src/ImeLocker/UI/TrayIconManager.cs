namespace ImeLocker.UI;

using System.Drawing;
using System.Windows.Forms;
using ImeLocker.Core;
using ImeLocker.Native;

public sealed class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    private Icon? _idleIcon;
    private Icon? _englishIcon;
    private Icon? _chineseIcon;
    private ImeDisplayState _currentState = ImeDisplayState.Idle;

    public TrayIconManager(NotifyIcon notifyIcon)
    {
        _notifyIcon = notifyIcon;
        RegenerateIcons();
        _notifyIcon.Icon = _idleIcon;
    }

    /// <summary>Update tooltip and icon to reflect current window and IME state.</summary>
    public void UpdateStatus(string processName, string? groupName, ImeState? imeState)
    {
        // Update tooltip
        var text = groupName is not null
            ? $"ImeLocker - {processName} [{groupName}]"
            : $"ImeLocker - {processName} [默认]";
        // NotifyIcon.Text max 63 chars
        _notifyIcon.Text = text.Length > 63 ? text[..63] : text;

        // Update icon based on IME state
        var newState = ClassifyImeState(imeState);
        if (newState != _currentState)
        {
            _currentState = newState;
            _notifyIcon.Icon = _currentState switch
            {
                ImeDisplayState.English => _englishIcon,
                ImeDisplayState.Chinese => _chineseIcon,
                _ => _idleIcon,
            };
        }
    }

    public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(3000, title, text, icon);
    }

    /// <summary>Regenerate icons (e.g. after theme change).</summary>
    public void RegenerateIcons()
    {
        var oldIdle = _idleIcon;
        var oldEn = _englishIcon;
        var oldCn = _chineseIcon;

        _idleIcon = TrayIconGenerator.CreateIdleIcon();
        _englishIcon = TrayIconGenerator.CreateEnglishIcon();
        _chineseIcon = TrayIconGenerator.CreateChineseIcon();

        // Update current display
        _notifyIcon.Icon = _currentState switch
        {
            ImeDisplayState.English => _englishIcon,
            ImeDisplayState.Chinese => _chineseIcon,
            _ => _idleIcon,
        };

        oldIdle?.Dispose();
        oldEn?.Dispose();
        oldCn?.Dispose();
    }

    private static ImeDisplayState ClassifyImeState(ImeState? state)
    {
        if (state is null)
            return ImeDisplayState.Idle;

        // Check if conversion mode has the NATIVE flag set
        // When IME_CMODE_NATIVE is set, the IME is in native (Chinese) input mode
        bool isNativeMode = (state.ConversionMode & Constants.IME_CMODE_NATIVE) != 0;

        return isNativeMode ? ImeDisplayState.Chinese : ImeDisplayState.English;
    }

    public void Dispose()
    {
        _idleIcon?.Dispose();
        _englishIcon?.Dispose();
        _chineseIcon?.Dispose();
    }

    private enum ImeDisplayState
    {
        Idle,
        English,
        Chinese,
    }
}
