namespace ImeLocker.UI;

using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using ImeLocker.Config;
using ImeLocker.Core;
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
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "ImeLocker",
        };
        _trayIconManager = new TrayIconManager(_notifyIcon);

        BuildContextMenu();
        _notifyIcon.ContextMenuStrip = _contextMenu;
        _notifyIcon.DoubleClick += (_, _) => OpenConfigWindow();

        _orchestrator.WindowSwitched += OnWindowSwitched;
        _configManager.ConfigChanged += OnConfigChanged;
    }

    private void BuildContextMenu()
    {
        _contextMenu = new ContextMenuStrip();

        // "配置..." item
        var configItem = new ToolStripMenuItem("配置...");
        configItem.Click += (_, _) => OpenConfigWindow();
        _contextMenu.Items.Add(configItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // Dynamic group items will be rebuilt on menu opening
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
    /// Items between the two separators (index 1 and the separator before autostart) are dynamic.
    /// </summary>
    private void RebuildGroupItems()
    {
        // Remove old dynamic items: everything between index 2 and the second separator
        // Layout: [配置...][sep][...dynamic...][sep][开机自启][退出]
        // We keep first 2 items (配置, sep) and last 3 items (sep, 开机自启, 退出)
        while (_contextMenu.Items.Count > 5)
        {
            _contextMenu.Items.RemoveAt(2);
        }

        // Insert group items at index 2
        int insertIndex = 2;
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

        // Remove current process from any existing group
        foreach (var g in _configManager.Config.Groups)
        {
            g.Apps.RemoveAll(a => a.ProcessName.Equals(_currentProcessName, StringComparison.OrdinalIgnoreCase));
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
            g.Apps.RemoveAll(a => a.ProcessName.Equals(_currentProcessName, StringComparison.OrdinalIgnoreCase));
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

    private void OnWindowSwitched(WindowInfo window, AppGroup? group)
    {
        _syncContext.Post(_ =>
        {
            _currentProcessName = window.ProcessName;
            _currentGroup = group;
            _trayIconManager.UpdateStatus(window.ProcessName, group?.Name);
        }, null);
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
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _contextMenu.Dispose();
            _configWindow?.Close();
        }
        base.Dispose(disposing);
    }
}
