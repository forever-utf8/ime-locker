namespace ImeLocker.Native;

internal static class Constants
{
    // WinEvent constants
    public const uint EVENT_SYSTEM_FOREGROUND = 3;
    public const uint WINEVENT_OUTOFCONTEXT = 0;

    // IME message constants
    public const uint WM_IME_CONTROL = 0x0283;
    public const uint IMC_SETCONVERSIONMODE = 0x0002;
    public const uint IMC_GETCONVERSIONMODE = 0x0001;

    // IME conversion mode flags
    public const uint IME_CMODE_NATIVE = 0x0001;
    public const uint IME_CMODE_KATAKANA = 0x0002;
    public const uint IME_CMODE_FULLSHAPE = 0x0008;
    public const uint IME_CMODE_ROMAN = 0x0010;
    public const uint IME_CMODE_CHARCODE = 0x0020;

    // Process access rights
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    // Keyboard layout flags
    public const uint KLF_ACTIVATE = 0x00000001;

    // Accessibility object identifiers
    public const int OBJID_WINDOW = 0;
    public const int CHILDID_SELF = 0;
}
