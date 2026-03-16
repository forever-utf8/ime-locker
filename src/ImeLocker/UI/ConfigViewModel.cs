namespace ImeLocker.UI;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using ImeLocker.Config;

public record KeyboardLayoutInfo(string HklHex, string DisplayName);

public record ConversionModeOption(string DisplayName, ConversionModePreset Value);

public record ScannedProcessInfo(string ProcessName, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>Converts a process name to its cached display name.</summary>
[ValueConversion(typeof(string), typeof(string))]
public sealed class ProcessNameToDisplayConverter : IValueConverter
{
    public static readonly ProcessNameToDisplayConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string name ? ProcessDisplayNames.Get(name) : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Inverts a boolean value for binding.</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public static readonly InverseBoolConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}

/// <summary>
/// Converts a keyboard layout HKL hex string to a list of available conversion mode options.
/// </summary>
[ValueConversion(typeof(string), typeof(List<ConversionModeOption>))]
public sealed class HklToConversionModesConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hklHex = value as string ?? "";
        return ConfigViewModel.GetConversionModesForLayout(hklHex);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Simple ICommand implementation using delegates.
/// </summary>
public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => execute();
}

/// <summary>
/// ViewModel for the ConfigWindow, binding to <see cref="AppConfig"/>.
/// </summary>
public sealed class ConfigViewModel : INotifyPropertyChanged
{
    private readonly ConfigManager _configManager;

    private SwitchMode _defaultMode;
    private string _presetKeyboardLayout = "";
    private ConversionModePreset _presetConversionMode;
    private ObservableCollection<AppGroup> _groups = [];
    private AppGroup? _selectedGroup;
    private AppEntry? _selectedApp;
    private ObservableCollection<ScannedProcessInfo> _scannedProcesses = [];
    private ScannedProcessInfo? _selectedScannedProcess;
    private bool _isScanning;

    public ConfigViewModel(ConfigManager configManager)
    {
        _configManager = configManager;

        AddGroupCommand = new RelayCommand(AddGroup);
        RemoveGroupCommand = new RelayCommand(RemoveGroup, () => SelectedGroup is not null);
        AddAppToGroupCommand = new RelayCommand(AddAppToGroup, () => SelectedGroup is not null && SelectedScannedProcess?.ProcessName is not null);
        RemoveAppFromGroupCommand = new RelayCommand(RemoveAppFromGroup, () => SelectedGroup is not null && SelectedApp is not null);
        ScanProcessesCommand = new RelayCommand(ScanProcesses);
        SaveCommand = new RelayCommand(Save);

        LoadFromConfig();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? SaveCompleted;

    public SwitchMode DefaultMode
    {
        get => _defaultMode;
        set => SetField(ref _defaultMode, value);
    }

    public string PresetKeyboardLayout
    {
        get => _presetKeyboardLayout;
        set
        {
            if (SetField(ref _presetKeyboardLayout, value))
            {
                OnPropertyChanged(nameof(AvailableConversionModes));
                if (!ImePreset.IsImeLayout(value))
                    PresetConversionMode = ConversionModePreset.Alphanumeric;
            }
        }
    }

    public ConversionModePreset PresetConversionMode
    {
        get => _presetConversionMode;
        set => SetField(ref _presetConversionMode, value);
    }

    public ObservableCollection<AppGroup> Groups
    {
        get => _groups;
        set => SetField(ref _groups, value);
    }

    public AppGroup? SelectedGroup
    {
        get => _selectedGroup;
        set => SetField(ref _selectedGroup, value);
    }

    public AppEntry? SelectedApp
    {
        get => _selectedApp;
        set => SetField(ref _selectedApp, value);
    }

    public ObservableCollection<ScannedProcessInfo> ScannedProcesses
    {
        get => _scannedProcesses;
        set => SetField(ref _scannedProcesses, value);
    }

    public ScannedProcessInfo? SelectedScannedProcess
    {
        get => _selectedScannedProcess;
        set => SetField(ref _selectedScannedProcess, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        set => SetField(ref _isScanning, value);
    }

    public ICommand AddGroupCommand { get; }
    public ICommand RemoveGroupCommand { get; }
    public ICommand AddAppToGroupCommand { get; }
    public ICommand RemoveAppFromGroupCommand { get; }
    public ICommand ScanProcessesCommand { get; }
    public ICommand SaveCommand { get; }

    public List<KeyboardLayoutInfo> InstalledKeyboardLayouts { get; } = GetInstalledKeyboardLayouts();

    public List<ConversionModeOption> AvailableConversionModes =>
        GetConversionModesForLayout(PresetKeyboardLayout);

    public static List<ConversionModeOption> GetConversionModesForLayout(string hklHex)
    {
        if (ImePreset.IsImeLayout(hklHex))
            return [new("中文", ConversionModePreset.Native), new("英文", ConversionModePreset.Alphanumeric)];
        return [new("默认", ConversionModePreset.Alphanumeric)];
    }

    private static List<KeyboardLayoutInfo> GetInstalledKeyboardLayouts()
    {
        var layouts = new List<KeyboardLayoutInfo>();
        foreach (InputLanguage lang in InputLanguage.InstalledInputLanguages)
        {
            var hklValue = lang.Handle.ToInt64() & 0xFFFFFFFF;
            var hex = $"0x{hklValue:X8}";
            layouts.Add(new KeyboardLayoutInfo(hex, $"{lang.LayoutName} ({hex})"));
        }
        return layouts;
    }

    private void LoadFromConfig()
    {
        var config = _configManager.Config;

        // Ensure all HKL values used in config are present in the layout list
        EnsureLayoutInList(config.PresetIme.KeyboardLayout);
        foreach (var g in config.Groups)
        {
            if (g.Preset is not null)
                EnsureLayoutInList(g.Preset.KeyboardLayout);
        }

        DefaultMode = config.DefaultMode;
        PresetKeyboardLayout = config.PresetIme.KeyboardLayout;
        PresetConversionMode = config.PresetIme.ConversionMode;
        Groups = new ObservableCollection<AppGroup>(config.Groups);
        SelectedGroup = Groups.FirstOrDefault();
    }

    private void EnsureLayoutInList(string hklHex)
    {
        if (InstalledKeyboardLayouts.Any(l => l.HklHex.Equals(hklHex, StringComparison.OrdinalIgnoreCase)))
            return;
        InstalledKeyboardLayouts.Add(new KeyboardLayoutInfo(hklHex, hklHex));
    }

    private void AddGroup()
    {
        var group = new AppGroup
        {
            Name = $"新分组 {Groups.Count + 1}",
            Mode = SwitchMode.Preset,
            Preset = new ImePreset(),
            Apps = [],
        };
        Groups.Add(group);
        SelectedGroup = group;
    }

    private void RemoveGroup()
    {
        if (SelectedGroup is null) return;
        Groups.Remove(SelectedGroup);
        SelectedGroup = null;
    }

    private void AddAppToGroup()
    {
        if (SelectedGroup is null || SelectedScannedProcess is null) return;
        var processName = SelectedScannedProcess.ProcessName;

        if (SelectedGroup.Apps.Any(a => a.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase)))
            return;

        SelectedGroup.Apps.Add(new AppEntry { ProcessName = processName });
    }

    private void RemoveAppFromGroup()
    {
        if (SelectedGroup is null || SelectedApp is null) return;
        SelectedGroup.Apps.Remove(SelectedApp);
        SelectedApp = null;
    }

    private async void ScanProcesses()
    {
        IsScanning = true;
        try
        {
            var processes = await Task.Run(() => ProcessScanner.ScanRunningProcesses());
            ScannedProcesses = new ObservableCollection<ScannedProcessInfo>(
                processes.Select(p =>
                {
                    var display = ProcessDisplayNames.Get(p.ProcessName);
                    var label = display != p.ProcessName ? $"{display} ({p.ProcessName})" : p.ProcessName;
                    return new ScannedProcessInfo(p.ProcessName, label);
                }));
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void Save()
    {
        var config = new AppConfig
        {
            Version = 1,
            DefaultMode = DefaultMode,
            PresetIme = new ImePreset
            {
                KeyboardLayout = PresetKeyboardLayout,
                ConversionMode = PresetConversionMode,
            },
            Groups = [.. Groups],
        };
        _configManager.Save(config);
        SaveCompleted?.Invoke();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
