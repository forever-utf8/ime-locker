namespace ImeLocker.Config;

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Serilog;

public static class ProcessScanner
{
    /// <summary>
    /// Scan all running processes that have a visible main window, returning distinct entries sorted alphabetically.
    /// </summary>
    public static List<(string ProcessName, string? WindowTitle)> ScanRunningProcesses()
    {
        try
        {
            return [.. Process.GetProcesses()
                .Select(p =>
                {
                    try
                    {
                        return string.IsNullOrEmpty(p.MainWindowTitle)
                            ? null
                            : (Name: p.ProcessName, Title: (string?)p.MainWindowTitle);
                    }
                    catch (Exception)
                    {
                        // Access denied for system processes — ignore
                        return null;
                    }
                    finally
                    {
                        p.Dispose();
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
