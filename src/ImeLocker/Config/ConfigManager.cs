namespace ImeLocker.Config;

using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public sealed class ConfigManager : IDisposable
{
    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ImeLocker");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.yaml");

    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;
    private FileSystemWatcher? _watcher;
    private bool _disposed;

    public AppConfig Config { get; private set; } = new();

    public event Action? ConfigChanged;

    public ConfigManager()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithEnumNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        _serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithEnumNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        Load();
        StartWatching();
    }

    /// <summary>
    /// Find the <see cref="AppGroup"/> whose Apps list contains a matching processName (case-insensitive), or null.
    /// </summary>
    public AppGroup? FindGroup(string processName) =>
        Config.Groups.Find(g =>
            g.Apps.Any(a => a.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase)));

    /// <summary>Serialize and write config to disk.</summary>
    public void Save(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var yaml = _serializer.Serialize(config);
            File.WriteAllText(ConfigPath, yaml);
            Config = config;
            Log.Logger.Information("Config saved to {Path}", ConfigPath);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Failed to save config to {Path}", ConfigPath);
        }
    }

    /// <summary>Deserialize config from disk, or create default if not exists.</summary>
    public void Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                Log.Logger.Information("Config file not found, creating default at {Path}", ConfigPath);
                Config = CreateDefaultConfig();
                Save(Config);
                return;
            }

            var yaml = File.ReadAllText(ConfigPath);
            Config = _deserializer.Deserialize<AppConfig>(yaml) ?? new AppConfig();
            Log.Logger.Information("Config loaded from {Path}", ConfigPath);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Failed to load config from {Path}, using default", ConfigPath);
            Config = new AppConfig();
        }
    }

    private void StartWatching()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            _watcher = new FileSystemWatcher(ConfigDir, "config.yaml")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += OnConfigFileChanged;
            _watcher.Created += OnConfigFileChanged;
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Failed to start config file watcher");
        }
    }

    private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        Log.Logger.Information("Config file changed, reloading");
        // Small delay to avoid reading while the file is still being written
        Thread.Sleep(100);
        Load();
        ConfigChanged?.Invoke();
    }

    private static AppConfig CreateDefaultConfig()
    {
        var (defaultLayout, codeLayout) = PickDefaultLayouts();

        return new AppConfig
        {
            Version = 1,
            DefaultMode = SwitchMode.Remember,
            PresetIme = new ImePreset
            {
                KeyboardLayout = defaultLayout,
                ConversionMode = ImePreset.IsImeLayout(defaultLayout)
                    ? ConversionModePreset.Native
                    : ConversionModePreset.Alphanumeric,
            },
            Groups =
            [
                new AppGroup
                {
                    Name = "Code",
                    Mode = SwitchMode.Preset,
                    Preset = new ImePreset
                    {
                        KeyboardLayout = codeLayout,
                        ConversionMode = ConversionModePreset.Alphanumeric,
                    },
                    Apps =
                    [
                        new AppEntry { ProcessName = "Code" },
                        new AppEntry { ProcessName = "WindowsTerminal" },
                        new AppEntry { ProcessName = "idea64" },
                    ],
                },
            ],
        };
    }

    /// <summary>
    /// Pick keyboard layouts from installed input languages.
    /// Returns (defaultLayout for global preset, codeLayout for Code group).
    /// Code group prefers English; global preset prefers CJK IME if available.
    /// </summary>
    private static (string DefaultLayout, string CodeLayout) PickDefaultLayouts()
    {
        var layouts = InputLanguage.InstalledInputLanguages
            .Cast<InputLanguage>()
            .Select(lang =>
            {
                var hkl = lang.Handle.ToInt64() & 0xFFFFFFFF;
                return (Hex: $"0x{hkl:X8}", PrimaryLang: (int)(hkl & 0x3FF));
            })
            .ToList();

        if (layouts.Count == 0)
            return ("0x08040804", "0x04090409");

        // English layout: primary language 0x09 (LANG_ENGLISH)
        var english = layouts.FirstOrDefault(l => l.PrimaryLang == 0x09);
        // CJK IME layout: Chinese(0x04) / Japanese(0x11) / Korean(0x12)
        var cjk = layouts.FirstOrDefault(l => l.PrimaryLang is 0x04 or 0x11 or 0x12);

        var codeLayout = english.Hex ?? layouts[0].Hex;
        var defaultLayout = cjk.Hex ?? layouts[0].Hex;

        return (defaultLayout, codeLayout);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watcher?.Dispose();
    }
}
