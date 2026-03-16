namespace ImeLocker.Config;

using System.Collections.Generic;
using System.Diagnostics;
using ImeLocker.Native;

/// <summary>
/// Caches human-readable display names for processes by reading FileVersionInfo.FileDescription
/// from the executable. Falls back to process name if unavailable.
/// </summary>
public static class ProcessDisplayNames
{
    private static readonly Dictionary<string, string> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Get cached display name, or return processName as-is.</summary>
    public static string Get(string processName) =>
        Cache.TryGetValue(processName, out var name) ? name : processName;

    /// <summary>Resolve display name from a PID and cache it.</summary>
    public static void ResolveFromPid(uint pid, string processName)
    {
        if (Cache.ContainsKey(processName)) return;

        var exePath = GetProcessPath(pid);
        if (exePath is null) return;

        try
        {
            var info = FileVersionInfo.GetVersionInfo(exePath);
            if (!string.IsNullOrWhiteSpace(info.FileDescription))
                Cache[processName] = info.FileDescription;
        }
        catch
        {
            // FileVersionInfo may fail for some executables
        }
    }

    private static string? GetProcessPath(uint pid)
    {
        var hProcess = Kernel32.OpenProcess(Constants.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hProcess == 0) return null;
        try
        {
            var buffer = new char[1024];
            uint size = (uint)buffer.Length;
            return Kernel32.QueryFullProcessImageNameW(hProcess, 0, buffer, ref size)
                ? new string(buffer, 0, (int)size)
                : null;
        }
        finally
        {
            Kernel32.CloseHandle(hProcess);
        }
    }
}
