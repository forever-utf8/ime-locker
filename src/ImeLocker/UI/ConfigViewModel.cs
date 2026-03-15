namespace ImeLocker.UI;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ImeLocker.Config;

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
    private ObservableCollection<string> _scannedProcesses = [];
    private string? _selectedScannedProcess;

    public ConfigViewModel(ConfigManager configManager)
    {
        _configManager = configManager;

        AddGroupCommand = new RelayCommand(AddGroup);
        RemoveGroupCommand = new RelayCommand(RemoveGroup, () => SelectedGroup is not null);
        AddAppToGroupCommand = new RelayCommand(AddAppToGroup, () => SelectedGroup is not null && SelectedScannedProcess is not null);
        RemoveAppFromGroupCommand = new RelayCommand(RemoveAppFromGroup, () => SelectedGroup is not null && SelectedApp is not null);
        ScanProcessesCommand = new RelayCommand(ScanProcesses);
        SaveCommand = new RelayCommand(Save);

        LoadFromConfig();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public SwitchMode DefaultMode
    {
        get => _defaultMode;
        set => SetField(ref _defaultMode, value);
    }

    public string PresetKeyboardLayout
    {
        get => _presetKeyboardLayout;
        set => SetField(ref _presetKeyboardLayout, value);
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

    public ObservableCollection<string> ScannedProcesses
    {
        get => _scannedProcesses;
        set => SetField(ref _scannedProcesses, value);
    }

    public string? SelectedScannedProcess
    {
        get => _selectedScannedProcess;
        set => SetField(ref _selectedScannedProcess, value);
    }

    public ICommand AddGroupCommand { get; }
    public ICommand RemoveGroupCommand { get; }
    public ICommand AddAppToGroupCommand { get; }
    public ICommand RemoveAppFromGroupCommand { get; }
    public ICommand ScanProcessesCommand { get; }
    public ICommand SaveCommand { get; }

    private void LoadFromConfig()
    {
        var config = _configManager.Config;
        DefaultMode = config.DefaultMode;
        PresetKeyboardLayout = config.PresetIme.KeyboardLayout;
        PresetConversionMode = config.PresetIme.ConversionMode;
        Groups = new ObservableCollection<AppGroup>(config.Groups);
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

        // Avoid duplicates
        if (SelectedGroup.Apps.Exists(a => a.ProcessName.Equals(SelectedScannedProcess, StringComparison.OrdinalIgnoreCase)))
            return;

        SelectedGroup.Apps.Add(new AppEntry { ProcessName = SelectedScannedProcess });
        // Trigger UI refresh by re-selecting the group
        OnPropertyChanged(nameof(SelectedGroup));
    }

    private void RemoveAppFromGroup()
    {
        if (SelectedGroup is null || SelectedApp is null) return;
        SelectedGroup.Apps.Remove(SelectedApp);
        SelectedApp = null;
        OnPropertyChanged(nameof(SelectedGroup));
    }

    private void ScanProcesses()
    {
        var processes = ProcessScanner.ScanRunningProcesses();
        ScannedProcesses = new ObservableCollection<string>(processes.Select(p => p.ProcessName));
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
