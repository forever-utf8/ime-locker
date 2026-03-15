using ImeLocker.Native;
using Serilog;

namespace ImeLocker.Core.Adapters;

public sealed class WechatImeAdapter : IImeAdapter
{
    private static readonly string[] WechatProcessPatterns = ["WeChat", "WeChatApp", "WeChatAppEx"];

    public bool CanHandle(string processName)
    {
        return WechatProcessPatterns.Any(pattern =>
            processName.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    public ImeState? GetState(nint hwnd, uint threadId)
    {
        nint keyboardLayout = User32.GetKeyboardLayout(threadId);

        nint hImc = Imm32.ImmGetContext(hwnd);
        if (hImc == 0)
        {
            Log.Logger.Warning("[WeChat] ImmGetContext 失败，HWND: {Hwnd}, ThreadId: {ThreadId}", hwnd, threadId);
            return new ImeState(keyboardLayout, 0, 0);
        }

        try
        {
            if (!Imm32.ImmGetConversionStatus(hImc, out uint conversion, out uint sentence))
            {
                Log.Logger.Warning("[WeChat] ImmGetConversionStatus 失败，HWND: {Hwnd}", hwnd);
                return new ImeState(keyboardLayout, 0, 0);
            }

            var state = new ImeState(keyboardLayout, conversion, sentence);
            Log.Logger.Debug("[WeChat] GetState: {State}", state);
            return state;
        }
        finally
        {
            Imm32.ImmReleaseContext(hwnd, hImc);
        }
    }

    public bool SetState(nint hwnd, uint threadId, ImeState state)
    {
        Log.Logger.Debug("[WeChat] SetState: HWND={Hwnd}, ThreadId={ThreadId}, State={State}", hwnd, threadId, state);

        nint result = User32.ActivateKeyboardLayout(state.KeyboardLayout, Constants.KLF_ACTIVATE);
        if (result == 0)
        {
            Log.Logger.Warning("[WeChat] ActivateKeyboardLayout 失败，Layout: {Layout}", state.KeyboardLayout);
        }

        // 微信输入法不走标准 ImmSetConversionStatus，改用 PostMessage WM_IME_CONTROL
        bool posted = User32.PostMessageW(
            hwnd,
            Constants.WM_IME_CONTROL,
            (nint)Constants.IMC_SETCONVERSIONMODE,
            (nint)state.ConversionMode);

        if (!posted)
        {
            Log.Logger.Warning("[WeChat] PostMessageW WM_IME_CONTROL 失败，HWND: {Hwnd}", hwnd);
            return false;
        }

        Log.Logger.Debug("[WeChat] SetState 成功（通过 PostMessage）");
        return true;
    }
}
