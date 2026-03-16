namespace ImeLocker.UI;

using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ImeLocker.Config;
using ImeLocker.Core;
using ImeLocker.Native;
using Microsoft.Win32;

/// <summary>
/// ApplicationContext that manages the system tray icon, context menu, and tray interactions.
/// </summary>
public sealed class SystemTrayApp : ApplicationContext
{
    private const string AutoStartRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AutoStartValueName = "ImeLocker";

    private readonly ConfigManager _configManager;
    private readonly AppOrchestrator _orchestrator;
    private readonly NotifyIcon _notifyIcon;
    private readonly TrayIconManager _trayIconManager;
    private readonly SynchronizationContext _syncContext;

    private ContextMenuStrip _contextMenu = null!;
    private ToolStripMenuItem _autoStartItem = null!;
    private ToolStripMenuItem _groupTitleItem = null!;
    private ConfigWindow? _configWindow;

    // Track current foreground window info for group assignment
    private string? _currentProcessName;
    private AppGroup? _currentGroup;

    public SystemTrayApp(ConfigManager configManager, AppOrchestrator orchestrator)
    {
        _configManager = configManager;
        _orchestrator = orchestrator;
        _syncContext = SynchronizationContext.Current ?? new SynchronizationContext();

        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "ImeLocker",
        };
        _trayIconManager = new TrayIconManager(_notifyIcon);

        BuildContextMenu();
        _notifyIcon.ContextMenuStrip = _contextMenu;
        _notifyIcon.DoubleClick += (_, _) => OpenConfigWindow();
        _notifyIcon.MouseMove += (_, _) => CaptureCurrentForeground();

        _orchestrator.WindowSwitched += OnWindowSwitched;
        _configManager.ConfigChanged += OnConfigChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    private void BuildContextMenu()
    {
        _contextMenu = new ContextMenuStrip();

        // "配置..." item
        var configItem = new ToolStripMenuItem("配置...");
        configItem.Click += (_, _) => OpenConfigWindow();
        _contextMenu.Items.Add(configItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // Dynamic title + group items, rebuilt on menu opening
        _groupTitleItem = new ToolStripMenuItem { Enabled = false };
        _contextMenu.Items.Add(_groupTitleItem);
        _contextMenu.Opening += (_, _) => RebuildGroupItems();

        _contextMenu.Items.Add(new ToolStripSeparator());

        // "开机自启" checkbox
        _autoStartItem = new ToolStripMenuItem("开机自启")
        {
            CheckOnClick = true,
            Checked = IsAutoStartEnabled(),
        };
        _autoStartItem.CheckedChanged += (_, _) => SetAutoStart(_autoStartItem.Checked);
        _contextMenu.Items.Add(_autoStartItem);

        // "退出"
        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (_, _) =>
        {
            _notifyIcon.Visible = false;
            Application.Exit();
        };
        _contextMenu.Items.Add(exitItem);
    }

    /// <summary>
    /// Rebuild the dynamic group section of the context menu each time it opens.
    /// Layout: [配置...][sep][title][...groups...][新建分组...][sep][开机自启][退出]
    /// Title and groups are rebuilt; title is at index 2 (static slot), groups start at 3.
    /// </summary>
    private void RebuildGroupItems()
    {
        _groupTitleItem.Text = _currentProcessName is not null
            ? $"为 {ProcessDisplayNames.Get(_currentProcessName)} 切换方案"
            : "切换方案";

        // Remove old dynamic items after the title (index 3) up to the second separator
        // Static items: [配置...][sep][title]...[sep][开机自启][退出] = keep first 3 + last 3
        while (_contextMenu.Items.Count > 6)
        {
            _contextMenu.Items.RemoveAt(3);
        }

        int insertIndex = 3;
        foreach (var group in _configManager.Config.Groups)
        {
            var groupItem = new ToolStripMenuItem(group.Name)
            {
                Checked = _currentGroup is not null && _currentGroup.Name == group.Name,
                Tag = group,
            };
            groupItem.Click += OnGroupItemClicked;
            _contextMenu.Items.Insert(insertIndex++, groupItem);
        }

        // "新建分组..."
        var newGroupItem = new ToolStripMenuItem("新建分组...");
        newGroupItem.Click += OnNewGroupClicked;
        _contextMenu.Items.Insert(insertIndex, newGroupItem);
    }

    private void OnGroupItemClicked(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem { Tag: AppGroup targetGroup } || _currentProcessName is null)
            return;

        // Remove process from any existing group
        foreach (var g in _configManager.Config.Groups)
        {
            foreach (var a in g.Apps.Where(a => a.ProcessName.Equals(_currentProcessName, StringComparison.OrdinalIgnoreCase)).ToList())
                g.Apps.Remove(a);
        }

        // Add to target group
        targetGroup.Apps.Add(new AppEntry { ProcessName = _currentProcessName });

        _configManager.Save(_configManager.Config);
        _trayIconManager.ShowBalloon("ImeLocker", $"已将 {_currentProcessName} 移至分组 [{targetGroup.Name}]");
    }

    private void OnNewGroupClicked(object? sender, EventArgs e)
    {
        if (_currentProcessName is null) return;

        var groupName = PromptForInput("新建分组", "请输入分组名称：");
        if (string.IsNullOrWhiteSpace(groupName)) return;

        var newGroup = new AppGroup
        {
            Name = groupName,
            Mode = SwitchMode.Preset,
            Preset = new ImePreset(),
            Apps = [new AppEntry { ProcessName = _currentProcessName }],
        };

        // Remove from existing groups
        foreach (var g in _configManager.Config.Groups)
        {
            foreach (var a in g.Apps.Where(a => a.ProcessName.Equals(_currentProcessName, StringComparison.OrdinalIgnoreCase)).ToList())
                g.Apps.Remove(a);
        }

        _configManager.Config.Groups.Add(newGroup);
        _configManager.Save(_configManager.Config);
        _trayIconManager.ShowBalloon("ImeLocker", $"已创建分组 [{groupName}] 并添加 {_currentProcessName}");
    }

    /// <summary>Simple input dialog using WinForms.</summary>
    private static string? PromptForInput(string title, string prompt)
    {
        using var form = new Form
        {
            Text = title,
            Width = 350,
            Height = 150,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MaximizeBox = false,
            MinimizeBox = false,
        };

        var label = new Label { Text = prompt, Left = 10, Top = 15, Width = 310 };
        var textBox = new TextBox { Left = 10, Top = 40, Width = 310 };
        var okButton = new Button { Text = "确定", Left = 160, Top = 70, Width = 75, DialogResult = DialogResult.OK };
        var cancelButton = new Button { Text = "取消", Left = 245, Top = 70, Width = 75, DialogResult = DialogResult.Cancel };

        form.AcceptButton = okButton;
        form.CancelButton = cancelButton;
        form.Controls.AddRange([label, textBox, okButton, cancelButton]);

        return form.ShowDialog() == DialogResult.OK ? textBox.Text : null;
    }

    /// <summary>
    /// Capture the foreground process when the mouse hovers over the tray icon.
    /// At this point the foreground window hasn't changed, so it's reliable.
    /// </summary>
    private void CaptureCurrentForeground()
    {
        var hwnd = User32.GetForegroundWindow();
        if (hwnd == 0) return;
        User32.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return;
        try
        {
            using var p = Process.GetProcessById((int)pid);
            _currentProcessName = p.ProcessName;
            _currentGroup = _configManager.FindGroup(_currentProcessName);
            ProcessDisplayNames.ResolveFromPid(pid, _currentProcessName);
        }
        catch
        {
            // Process may have exited
        }
    }

    private void OnWindowSwitched(WindowInfo window, AppGroup? group, ImeState? imeState)
    {
        _syncContext.Post(_ =>
        {
            _trayIconManager.UpdateTooltip(window.ProcessName, group?.Name);
        }, null);
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
        {
            _syncContext.Post(_ => _trayIconManager.RegenerateIcon(), null);
        }
    }

    private void OnConfigChanged()
    {
        _syncContext.Post(_ =>
        {
            _autoStartItem.Checked = IsAutoStartEnabled();
        }, null);
    }

    private void OpenConfigWindow()
    {
        if (_configWindow is { IsLoaded: true })
        {
            _configWindow.Show();
            _configWindow.Activate();
            return;
        }

        _configWindow = new ConfigWindow(_configManager);
        _configWindow.Show();
    }

    private static bool IsAutoStartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(AutoStartRegistryKey, false);
        return key?.GetValue(AutoStartValueName) is not null;
    }

    private static void SetAutoStart(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(AutoStartRegistryKey, true);
        if (key is null) return;

        if (enable)
        {
            var exePath = Environment.ProcessPath ?? "";
            key.SetValue(AutoStartValueName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(AutoStartValueName, false);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _orchestrator.WindowSwitched -= OnWindowSwitched;
            _configManager.ConfigChanged -= OnConfigChanged;
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _trayIconManager.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _contextMenu.Dispose();
            _configWindow?.Close();
        }
        base.Dispose(disposing);
    }
}
