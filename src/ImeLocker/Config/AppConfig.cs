namespace ImeLocker.Config;

using System.Globalization;
using YamlDotNet.Serialization;

public enum SwitchMode { Preset, Remember }

public enum ConversionModePreset { Native, Alphanumeric }

public class ImePreset
{
    [YamlMember(Alias = "keyboardLayout")]
    public string KeyboardLayout { get; set; } = "0x08040804";

    [YamlMember(Alias = "conversionMode")]
    public ConversionModePreset ConversionMode { get; set; } = ConversionModePreset.Native;

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
}

public class AppEntry
{
    [YamlMember(Alias = "processName")]
    public string ProcessName { get; set; } = "";
}

public class AppGroup
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "";

    [YamlMember(Alias = "mode")]
    public SwitchMode Mode { get; set; } = SwitchMode.Preset;

    [YamlMember(Alias = "preset")]
    public ImePreset? Preset { get; set; }

    [YamlMember(Alias = "apps")]
    public List<AppEntry> Apps { get; set; } = [];
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
