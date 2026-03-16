using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ImeLocker.Config;
using Serilog;

namespace ImeLocker.Core;

/// <summary>
/// Core orchestrator: responds to foreground window changes, applies IME rules.
/// </summary>
public sealed class AppOrchestrator : IDisposable
{
    // Dependencies
    private readonly WindowMonitor _windowMonitor;
    private readonly ImeController _imeController;
    private readonly ConfigManager _configManager;

    // State
    private WindowInfo? _currentWindow;
    private readonly Dictionary<string, ImeState> _rememberedStates = new(StringComparer.OrdinalIgnoreCase); // key = processName
    private readonly System.Timers.Timer _cleanupTimer;

    // Events for UI
    public event Action<WindowInfo, AppGroup?>? WindowSwitched; // fired after handling switch, with matched group

    public AppOrchestrator(WindowMonitor windowMonitor, ImeController imeController, ConfigManager configManager)
    {
        _windowMonitor = windowMonitor;
        _imeController = imeController;
        _configManager = configManager;
        _windowMonitor.ForegroundWindowChanged += OnForegroundWindowChanged;

        // Periodic cleanup of remembered states for exited processes (every 5 min)
        _cleanupTimer = new System.Timers.Timer(300_000);
        _cleanupTimer.Elapsed += (_, _) => CleanupRememberedStates();
        _cleanupTimer.Start();
    }

    private async void OnForegroundWindowChanged(WindowInfo newWindow)
    {
        // 1. Save current window's IME state if it was in "Remember" mode
        if (_currentWindow is not null)
        {
            var oldGroup = _configManager.FindGroup(_currentWindow.ProcessName);
            var mode = oldGroup?.Mode ?? _configManager.Config.DefaultMode;
            if (mode == SwitchMode.Remember)
            {
                var currentState = _imeController.GetState(_currentWindow.Hwnd, _currentWindow.ThreadId, _currentWindow.ProcessName);
                if (currentState is not null)
                {
                    _rememberedStates[_currentWindow.ProcessName] = currentState;
                    Log.Logger.Debug("Saved IME state for {Process}: {@State}", _currentWindow.ProcessName, currentState);
                }
            }
        }

        _currentWindow = newWindow;

        // 2. Delay to let the window fully activate
        await Task.Delay(80);

        // 3. Find matching group
        var group = _configManager.FindGroup(newWindow.ProcessName);
        var switchMode = group?.Mode ?? _configManager.Config.DefaultMode;

        // 4. Apply IME state
        switch (switchMode)
        {
            case SwitchMode.Preset:
                var preset = group?.Preset ?? _configManager.Config.PresetIme;
                var presetState = new ImeState(preset.GetKeyboardLayoutHandle(), preset.GetConversionModeFlag(), 0);
                ApplyWithRetry(newWindow, presetState);
                break;

            case SwitchMode.Remember:
                if (_rememberedStates.TryGetValue(newWindow.ProcessName, out var remembered))
                {
                    ApplyWithRetry(newWindow, remembered);
                }
                // else: first time seeing this app, leave IME as-is
                break;
        }

        // 5. Notify UI
        WindowSwitched?.Invoke(newWindow, group);
    }

    private void ApplyWithRetry(WindowInfo window, ImeState state, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            if (_imeController.SetState(window.Hwnd, window.ThreadId, window.ProcessName, state))
            {
                Log.Logger.Information("Applied IME state to {Process}: {@State} (attempt {Attempt})", window.ProcessName, state, i + 1);
                return;
            }
            Thread.Sleep(50);
        }
        Log.Logger.Warning("Failed to apply IME state to {Process} after {Retries} attempts", window.ProcessName, maxRetries);
    }

    private void CleanupRememberedStates()
    {
        // Remove entries for processes that are no longer running
        // Note: WindowInfo.ProcessName uses GetFileNameWithoutExtension (no .exe suffix)
        var runningProcessNames = new HashSet<string>(
            System.Diagnostics.Process.GetProcesses().Select(p =>
            {
                try { return p.ProcessName; }
                catch { return ""; }
            }),
            StringComparer.OrdinalIgnoreCase
        );

        var toRemove = _rememberedStates.Keys.Where(k => !runningProcessNames.Contains(k)).ToList();
        foreach (var key in toRemove)
        {
            _rememberedStates.Remove(key);
            Log.Logger.Debug("Cleaned up remembered state for exited process: {Process}", key);
        }
    }

    public void Dispose()
    {
        _windowMonitor.ForegroundWindowChanged -= OnForegroundWindowChanged;
        _cleanupTimer.Dispose();
    }
}
