using ImeLocker.Native;
using Serilog;

namespace ImeLocker.Core;

public sealed class WindowMonitor : IDisposable
{
    private nint _hookHandle;
    private WinEventDelegate? _winEventDelegate;
    private bool _disposed;

    public event Action<WindowInfo>? ForegroundWindowChanged;

    public void Start()
    {
        if (_hookHandle != 0)
        {
            Log.Logger.Warning("WindowMonitor 已经在运行中");
            return;
        }

        // 持有委托引用，防止 GC 回收
        _winEventDelegate = OnWinEvent;

        _hookHandle = User32.SetWinEventHook(
            Constants.EVENT_SYSTEM_FOREGROUND,
            Constants.EVENT_SYSTEM_FOREGROUND,
            0,
            _winEventDelegate,
            0,
            0,
            Constants.WINEVENT_OUTOFCONTEXT);

        if (_hookHandle == 0)
        {
            Log.Logger.Error("SetWinEventHook 失败");
            return;
        }

        Log.Logger.Information("WindowMonitor 已启动，Hook 句柄: {HookHandle}", _hookHandle);
    }

    public void Stop()
    {
        if (_hookHandle == 0)
            return;

        if (User32.UnhookWinEvent(_hookHandle))
        {
            Log.Logger.Information("WindowMonitor 已停止，已释放 Hook 句柄: {HookHandle}", _hookHandle);
        }
        else
        {
            Log.Logger.Error("UnhookWinEvent 失败，Hook 句柄: {HookHandle}", _hookHandle);
        }

        _hookHandle = 0;
        _winEventDelegate = null;
    }

    private void OnWinEvent(
        nint hWinEventHook,
        uint eventType,
        nint hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (hwnd == 0)
            return;

        uint threadId = User32.GetWindowThreadProcessId(hwnd, out uint processId);
        if (threadId == 0)
        {
            Log.Logger.Warning("GetWindowThreadProcessId 失败，HWND: {Hwnd}", hwnd);
            return;
        }

        string? exePath = GetProcessExecutablePath(processId);
        string processName = exePath is not null
            ? Path.GetFileNameWithoutExtension(exePath)
            : $"<unknown:{processId}>";

        var info = new WindowInfo(hwnd, processId, threadId, processName, exePath);
        Log.Logger.Debug("前台窗口切换: {WindowInfo}", info);

        ForegroundWindowChanged?.Invoke(info);
    }

    private static string? GetProcessExecutablePath(uint processId)
    {
        nint hProcess = Kernel32.OpenProcess(Constants.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (hProcess == 0)
        {
            Log.Logger.Warning("OpenProcess 失败，PID: {ProcessId}", processId);
            return null;
        }

        try
        {
            var buffer = new char[1024];
            uint size = (uint)buffer.Length;

            if (Kernel32.QueryFullProcessImageNameW(hProcess, 0, buffer, ref size))
            {
                return new string(buffer, 0, (int)size);
            }

            Log.Logger.Warning("QueryFullProcessImageNameW 失败，PID: {ProcessId}", processId);
            return null;
        }
        finally
        {
            Kernel32.CloseHandle(hProcess);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _disposed = true;
    }
}
