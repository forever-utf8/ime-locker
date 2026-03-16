namespace ImeLocker.Config;

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ImeLocker.Native;
using Serilog;

public static class ProcessScanner
{
    /// <summary>
    /// Scan all processes that own at least one visible top-level window,
    /// returning distinct entries sorted alphabetically.
    /// Uses EnumWindows for reliable detection of tray/background apps like WeChat.
    /// </summary>
    public static List<(string ProcessName, string? WindowTitle)> ScanRunningProcesses()
    {
        try
        {
            var pidSet = new HashSet<uint>();

            User32.EnumWindows((hWnd, _) =>
            {
                User32.GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid != 0)
                    pidSet.Add(pid);
                return true;
            }, 0);

            return [.. pidSet
                .Select(pid =>
                {
                    try
                    {
                        using var p = Process.GetProcessById((int)pid);
                        ProcessDisplayNames.ResolveFromPid(pid, p.ProcessName);
                        return ((string Name, string? Title)?)(p.ProcessName, p.MainWindowTitle);
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Where(x => x is not null)
                .Select(x => x!.Value)
                .DistinctBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)];
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Failed to scan running processes");
            return [];
        }
    }
}
