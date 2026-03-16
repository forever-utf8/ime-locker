using System.Collections.Generic;
using System.Linq;
using ImeLocker.Core.Adapters;
using Serilog;

namespace ImeLocker.Core;

public sealed class ImeController
{
    private readonly List<IImeAdapter> _adapters =
    [
        new WechatImeAdapter(),
        new StandardImeAdapter(),
    ];

    public ImeState? GetState(nint hwnd, uint threadId, string processName)
    {
        var adapter = FindAdapter(processName);
        Log.Logger.Debug("GetState 使用适配器: {Adapter}, 进程: {ProcessName}", adapter.GetType().Name, processName);
        return adapter.GetState(hwnd, threadId);
    }

    public bool SetState(nint hwnd, uint threadId, string processName, ImeState state)
    {
        var adapter = FindAdapter(processName);
        Log.Logger.Debug("SetState 使用适配器: {Adapter}, 进程: {ProcessName}", adapter.GetType().Name, processName);
        return adapter.SetState(hwnd, threadId, state);
    }

    private IImeAdapter FindAdapter(string processName)
    {
        // StandardImeAdapter.CanHandle 始终返回 true，所以一定能找到
        return _adapters.First(a => a.CanHandle(processName));
    }
}
