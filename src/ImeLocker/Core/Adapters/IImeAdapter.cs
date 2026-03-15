namespace ImeLocker.Core.Adapters;

public interface IImeAdapter
{
    bool CanHandle(string processName);
    ImeState? GetState(nint hwnd, uint threadId);
    bool SetState(nint hwnd, uint threadId, ImeState state);
}
