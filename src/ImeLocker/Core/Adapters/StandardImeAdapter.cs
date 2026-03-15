using ImeLocker.Native;
using Serilog;

namespace ImeLocker.Core.Adapters;

public sealed class StandardImeAdapter : IImeAdapter
{
    public bool CanHandle(string processName) => true;

    public ImeState? GetState(nint hwnd, uint threadId)
    {
        nint keyboardLayout = User32.GetKeyboardLayout(threadId);

        nint hImc = Imm32.ImmGetContext(hwnd);
        if (hImc == 0)
        {
            Log.Logger.Warning("[Standard] ImmGetContext 失败，HWND: {Hwnd}, ThreadId: {ThreadId}", hwnd, threadId);
            return new ImeState(keyboardLayout, 0, 0);
        }

        try
        {
            if (!Imm32.ImmGetConversionStatus(hImc, out uint conversion, out uint sentence))
            {
                Log.Logger.Warning("[Standard] ImmGetConversionStatus 失败，HWND: {Hwnd}", hwnd);
                return new ImeState(keyboardLayout, 0, 0);
            }

            var state = new ImeState(keyboardLayout, conversion, sentence);
            Log.Logger.Debug("[Standard] GetState: {State}", state);
            return state;
        }
        finally
        {
            Imm32.ImmReleaseContext(hwnd, hImc);
        }
    }

    public bool SetState(nint hwnd, uint threadId, ImeState state)
    {
        Log.Logger.Debug("[Standard] SetState: HWND={Hwnd}, ThreadId={ThreadId}, State={State}", hwnd, threadId, state);

        nint result = User32.ActivateKeyboardLayout(state.KeyboardLayout, Constants.KLF_ACTIVATE);
        if (result == 0)
        {
            Log.Logger.Warning("[Standard] ActivateKeyboardLayout 失败，Layout: {Layout}", state.KeyboardLayout);
        }

        nint hImc = Imm32.ImmGetContext(hwnd);
        if (hImc == 0)
        {
            Log.Logger.Warning("[Standard] ImmGetContext 失败（SetState），HWND: {Hwnd}", hwnd);
            return false;
        }

        try
        {
            if (!Imm32.ImmSetConversionStatus(hImc, state.ConversionMode, state.SentenceMode))
            {
                Log.Logger.Warning("[Standard] ImmSetConversionStatus 失败，HWND: {Hwnd}", hwnd);
                return false;
            }

            Log.Logger.Debug("[Standard] SetState 成功");
            return true;
        }
        finally
        {
            Imm32.ImmReleaseContext(hwnd, hImc);
        }
    }
}
