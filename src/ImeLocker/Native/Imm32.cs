using System.Runtime.InteropServices;

namespace ImeLocker.Native;

internal static partial class Imm32
{
    [LibraryImport("imm32.dll", SetLastError = true)]
    public static partial nint ImmGetContext(nint hWnd);

    [LibraryImport("imm32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ImmReleaseContext(nint hWnd, nint hIMC);

    [LibraryImport("imm32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ImmGetConversionStatus(nint hIMC, out uint conversion, out uint sentence);

    [LibraryImport("imm32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ImmSetConversionStatus(nint hIMC, uint conversion, uint sentence);
}
