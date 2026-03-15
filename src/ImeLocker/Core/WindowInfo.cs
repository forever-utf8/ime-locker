namespace ImeLocker.Core;

public record WindowInfo(nint Hwnd, uint ProcessId, uint ThreadId, string ProcessName, string? ExecutablePath);
