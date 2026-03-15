using System.Windows.Forms;
using ImeLocker.Config;
using ImeLocker.Core;
using ImeLocker.UI;
using Serilog;

Application.SetHighDpiMode(HighDpiMode.SystemAware);
Application.EnableVisualStyles();

using var mutex = new Mutex(true, @"Global\ImeLocker_SingleInstance", out bool createdNew);
if (!createdNew)
{
    MessageBox.Show("ImeLocker 已在运行中。", "ImeLocker", MessageBoxButtons.OK, MessageBoxIcon.Information);
    return;
}

var logPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "ImeLocker", "logs", "imelocker-.log");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(
        logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Log.Information("ImeLocker 启动中...");

    using var configManager = new ConfigManager();
    using var windowMonitor = new WindowMonitor();
    var imeController = new ImeController();
    using var orchestrator = new AppOrchestrator(windowMonitor, imeController, configManager);
    using var systemTrayApp = new SystemTrayApp(configManager, orchestrator);

    windowMonitor.Start();

    Log.Information("ImeLocker 已启动，进入消息循环");
    Application.Run(systemTrayApp);

    Log.Information("ImeLocker 正常退出");
}
catch (Exception ex)
{
    Log.Fatal(ex, "ImeLocker 发生未处理的异常");
    MessageBox.Show($"ImeLocker 发生严重错误，即将退出。\n\n{ex.Message}", "ImeLocker 错误",
        MessageBoxButtons.OK, MessageBoxIcon.Error);
}
finally
{
    Log.CloseAndFlush();
}
