namespace ImeLocker.Config;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using YamlDotNet.Serialization;

public enum SwitchMode { Preset, Remember }

public enum ConversionModePreset { Native, Alphanumeric }

public class ImePreset : INotifyPropertyChanged
{
    private string _keyboardLayout = "0x08040804";
    private ConversionModePreset _conversionMode = ConversionModePreset.Native;

    public event PropertyChangedEventHandler? PropertyChanged;

    [YamlMember(Alias = "keyboardLayout")]
    public string KeyboardLayout
    {
        get => _keyboardLayout;
        set
        {
            if (_keyboardLayout == value) return;
            _keyboardLayout = value;
            OnPropertyChanged();
            if (!IsImeLayout(value))
                ConversionMode = ConversionModePreset.Alphanumeric;
        }
    }

    [YamlMember(Alias = "conversionMode")]
    public ConversionModePreset ConversionMode
    {
        get => _conversionMode;
        set
        {
            if (_conversionMode == value) return;
            _conversionMode = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Parse KeyboardLayout hex string to nint.</summary>
    public nint GetKeyboardLayoutHandle() =>
        nint.Parse(KeyboardLayout.Replace("0x", ""), NumberStyles.HexNumber);

    /// <summary>Convert ConversionModePreset to uint flag value.</summary>
    public uint GetConversionModeFlag() => ConversionMode switch
    {
        ConversionModePreset.Native => 1, // IME_CMODE_NATIVE
        ConversionModePreset.Alphanumeric => 0,
        _ => 0,
    };

    /// <summary>
    /// Determines if the given HKL represents an IME layout (Chinese, Japanese, Korean)
    /// that supports conversion modes.
    /// </summary>
    public static bool IsImeLayout(string hklHex)
    {
        if (!long.TryParse(hklHex.Replace("0x", ""), NumberStyles.HexNumber, null, out var hkl))
            return false;
        var primaryLang = (int)(hkl & 0x3FF);
        return primaryLang is 0x04 or 0x11 or 0x12; // Chinese, Japanese, Korean
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public class AppEntry
{
    [YamlMember(Alias = "processName")]
    public string ProcessName { get; set; } = "";
}

public class AppGroup : INotifyPropertyChanged
{
    private SwitchMode _mode = SwitchMode.Preset;

    public event PropertyChangedEventHandler? PropertyChanged;

    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "";

    [YamlMember(Alias = "mode")]
    public SwitchMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Mode)));
        }
    }

    [YamlMember(Alias = "preset")]
    public ImePreset? Preset { get; set; }

    [YamlMember(Alias = "apps")]
    public ObservableCollection<AppEntry> Apps { get; set; } = [];
}

public class AppConfig
{
    [YamlMember(Alias = "version")]
    public int Version { get; set; } = 1;

    [YamlMember(Alias = "defaultMode")]
    public SwitchMode DefaultMode { get; set; } = SwitchMode.Preset;

    [YamlMember(Alias = "presetIme")]
    public ImePreset PresetIme { get; set; } = new();

    [YamlMember(Alias = "groups")]
    public List<AppGroup> Groups { get; set; } = [];
}
