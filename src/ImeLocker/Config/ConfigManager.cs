namespace ImeLocker.Config;

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
            g.Apps.Exists(a => a.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase)));

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

    private static AppConfig CreateDefaultConfig() => new()
    {
        Version = 1,
        DefaultMode = SwitchMode.Preset,
        PresetIme = new ImePreset
        {
            KeyboardLayout = "0x08040804",
            ConversionMode = ConversionModePreset.Native,
        },
        Groups =
        [
            new AppGroup
            {
                Name = "English Apps",
                Mode = SwitchMode.Preset,
                Preset = new ImePreset
                {
                    KeyboardLayout = "0x04090409",
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watcher?.Dispose();
    }
}
